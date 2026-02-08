using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusBot.Core.Entities;
using FocusBot.Core.Events;
using FocusBot.Core.Interfaces;
using TaskStatus = FocusBot.Core.Entities.TaskStatus;

namespace FocusBot.App.ViewModels;

public partial class KanbanBoardViewModel : ObservableObject
{
    private readonly ITaskRepository _repo;
    private readonly IWindowMonitorService _windowMonitor;
    private readonly ITimeTrackingService _timeTracking;
    private readonly INavigationService _navigationService;
    private readonly IOpenAIService _openAIService;
    private readonly ISettingsService _settingsService;

    private long _taskElapsedSeconds;
    private int _secondsSinceLastPersist;
    private const int PersistIntervalSeconds = 5;

    public ObservableCollection<UserTask> ToDoTasks { get; } = new();
    public ObservableCollection<UserTask> InProgressTasks { get; } = new();
    public ObservableCollection<UserTask> DoneTasks { get; } = new();

    private string _newTaskDescription = string.Empty;
    public string NewTaskDescription
    {
        get => _newTaskDescription;
        set => SetProperty(ref _newTaskDescription, value);
    }

    private bool _showAddTaskInput;
    public bool ShowAddTaskInput
    {
        get => _showAddTaskInput;
        set => SetProperty(ref _showAddTaskInput, value);
    }

    private string _currentProcessName = string.Empty;
    public string CurrentProcessName
    {
        get => _currentProcessName;
        set => SetProperty(ref _currentProcessName, value);
    }

    private string _currentWindowTitle = string.Empty;
    public string CurrentWindowTitle
    {
        get => _currentWindowTitle;
        set => SetProperty(ref _currentWindowTitle, value);
    }

    private bool _isMonitoring;
    public bool IsMonitoring
    {
        get => _isMonitoring;
        set => SetProperty(ref _isMonitoring, value);
    }

    private int _focusScore;
    public int FocusScore
    {
        get => _focusScore;
        set => SetProperty(ref _focusScore, value);
    }

    private string _focusReason = string.Empty;
    public string FocusReason
    {
        get => _focusReason;
        set => SetProperty(ref _focusReason, value);
    }

    public bool IsFocusScoreVisible => IsMonitoring && HasValidFocusData();

    private string _taskElapsedTime = "00:00:00";
    public string TaskElapsedTime
    {
        get => _taskElapsedTime;
        set => SetProperty(ref _taskElapsedTime, value);
    }

    public KanbanBoardViewModel(
        ITaskRepository repo,
        IWindowMonitorService windowMonitor,
        ITimeTrackingService timeTracking,
        INavigationService navigationService,
        IOpenAIService openAIService,
        ISettingsService settingsService
    )
    {
        _repo = repo;
        _windowMonitor = windowMonitor;
        _timeTracking = timeTracking;
        _navigationService = navigationService;
        _openAIService = openAIService;
        _settingsService = settingsService;
        _windowMonitor.ForegroundWindowChanged += OnForegroundWindowChanged;
        _timeTracking.Tick += OnTimeTrackingTick;
        _ = LoadBoardAsync();
    }

    private static string FormatElapsed(long totalSeconds)
    {
        var hours = (int)(totalSeconds / 3600);
        var minutes = (int)((totalSeconds % 3600) / 60);
        var seconds = (int)(totalSeconds % 60);
        return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
    }

    private void OnTimeTrackingTick(object? sender, EventArgs e)
    {
        if (InProgressTasks.Count == 0)
            return;
        _taskElapsedSeconds++;
        TaskElapsedTime = FormatElapsed(_taskElapsedSeconds);
        _secondsSinceLastPersist++;
        if (_secondsSinceLastPersist >= PersistIntervalSeconds)
        {
            _secondsSinceLastPersist = 0;
            var taskId = InProgressTasks[0].TaskId;
            _ = PersistElapsedTimeAsync(taskId);
        }
    }

    private async Task PersistElapsedTimeAsync(string taskId)
    {
        await _repo.UpdateElapsedTimeAsync(taskId, _taskElapsedSeconds);
    }

    private void OnForegroundWindowChanged(object? sender, ForegroundWindowChangedEventArgs e)
    {
        CurrentProcessName = e.ProcessName;
        CurrentWindowTitle = e.WindowTitle;
        FocusScore = 0;
        FocusReason = string.Empty;
        OnPropertyChanged(nameof(IsFocusScoreVisible));

        if (InProgressTasks.Count == 0)
            return;
        var task = InProgressTasks[0];
        _ = ClassifyAndUpdateFocusAsync(
            task.Description,
            task.Context,
            e.ProcessName,
            e.WindowTitle
        );
    }

    private async Task ClassifyAndUpdateFocusAsync(
        string taskDescription,
        string? taskContext,
        string processName,
        string windowTitle
    )
    {
        var result = await _openAIService.ClassifyAlignmentAsync(
            taskDescription,
            taskContext,
            processName,
            windowTitle
        );
        if (result != null)
        {
            FocusScore = result.Score;
            FocusReason = result.Reason;
        }
        OnPropertyChanged(nameof(IsFocusScoreVisible));
    }

    private async Task LoadBoardAsync()
    {
        ToDoTasks.Clear();
        InProgressTasks.Clear();
        DoneTasks.Clear();

        foreach (var t in await _repo.GetToDoTasksAsync())
            ToDoTasks.Add(t);
        var inProgress = await _repo.GetInProgressTaskAsync();
        if (inProgress != null)
            InProgressTasks.Add(inProgress);
        foreach (var t in await _repo.GetDoneTasksAsync())
            DoneTasks.Add(t);

        if (HasActiveTask())
        {
            _taskElapsedSeconds = InProgressTasks[0].TotalElapsedSeconds;
            TaskElapsedTime = FormatElapsed(_taskElapsedSeconds);
            _secondsSinceLastPersist = 0;
            StartMonitoring();
        }
        else
            StopMonitoringAndResetFocusState();
        IsMonitoring = InProgressTasks.Count > 0;
    }

    [RelayCommand]
    private void ToggleAddTask() => ShowAddTaskInput = !ShowAddTaskInput;

    [RelayCommand]
    private void OpenSettings() => _navigationService.NavigateToSettings();

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task AddTaskAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTaskDescription))
            return;
        var task = await _repo.AddTaskAsync(NewTaskDescription.Trim());
        ToDoTasks.Add(task);
        CloseNewTaskPopup();
    }

    private void CloseNewTaskPopup()
    {
        NewTaskDescription = string.Empty;
        ShowAddTaskInput = false;
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task MoveToInProgressAsync(string? taskId)
    {
        if (string.IsNullOrEmpty(taskId))
            return;
        await _repo.SetStatusToAsync(taskId, TaskStatus.InProgress);
        await LoadBoardAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task MoveToDoneAsync(string? taskId)
    {
        if (string.IsNullOrEmpty(taskId))
            return;
        await UpdateTaskStatusAndDuration(taskId, TaskStatus.Done);
        await LoadBoardAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task MoveToToDoAsync(string? taskId)
    {
        if (string.IsNullOrEmpty(taskId))
            return;
        await _repo.UpdateElapsedTimeAsync(taskId, _taskElapsedSeconds);
        await _repo.SetStatusToAsync(taskId, TaskStatus.ToDo);
        await LoadBoardAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task DeleteTaskAsync(string? taskId)
    {
        if (string.IsNullOrEmpty(taskId))
            return;
        await _repo.DeleteTaskAsync(taskId);
        await LoadBoardAsync();
    }

    /// <summary>
    /// Move task to the given status (used by drag-and-drop). Status name: ToDo, InProgress, Done.
    /// </summary>
    public async Task MoveToStatusAsync(string taskId, string status)
    {
        var task = await _repo.GetByIdAsync(taskId);
        if (task == null)
            return;
        var statusEnum = Enum.Parse<TaskStatus>(status);
        var isMovingCurrentInProgress = HasActiveTask() && InProgressTasks[0].TaskId == taskId;
        if (!isMovingCurrentInProgress)
            _taskElapsedSeconds = task.TotalElapsedSeconds;
        await UpdateTaskStatusAndDuration(taskId, statusEnum);
        await LoadBoardAsync();
    }

    private async Task UpdateTaskStatusAndDuration(string taskId, TaskStatus statusEnum)
    {
        await _repo.UpdateElapsedTimeAsync(taskId, _taskElapsedSeconds);
        await _repo.SetStatusToAsync(taskId, statusEnum);
    }

    private bool HasValidFocusData() => FocusScore > 0 || !string.IsNullOrEmpty(FocusReason);

    private bool HasActiveTask() => InProgressTasks.Count > 0;

    private void StartMonitoring()
    {
        _windowMonitor.Start();
        _timeTracking.Start();
    }

    private void StopMonitoringAndResetFocusState()
    {
        _windowMonitor.Stop();
        _timeTracking.Stop();
        _taskElapsedSeconds = 0;
        TaskElapsedTime = FormatElapsed(0);
        _secondsSinceLastPersist = 0;
        ResetFocusState();
    }

    private void ResetFocusState()
    {
        CurrentProcessName = string.Empty;
        CurrentWindowTitle = string.Empty;
        FocusScore = 0;
        FocusReason = string.Empty;
        OnPropertyChanged(nameof(IsFocusScoreVisible));
    }
}

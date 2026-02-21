using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusBot.Core.Entities;
using FocusBot.Core.Events;
using FocusBot.Core.Helpers;
using FocusBot.Core.Interfaces;
using TaskStatus = FocusBot.Core.Entities.TaskStatus;

namespace FocusBot.App.ViewModels;

public partial class KanbanBoardViewModel : ObservableObject
{
    private readonly ITaskRepository _repo;
    private readonly IWindowMonitorService _windowMonitor;
    private readonly ITimeTrackingService _timeTracking;
    private readonly IIdleDetectionService _idleDetection;
    private readonly INavigationService _navigationService;
    private readonly ILlmService _llmService;
    private readonly ISettingsService _settingsService;
    private readonly IFocusScoreService _focusScoreService;

    private static readonly string FocusBotProcessName = GetFocusBotProcessName();

    private long _taskElapsedSeconds;
    private int _secondsSinceLastPersist;
    private const int PersistIntervalSeconds = 5;

    public ObservableCollection<UserTask> ToDoTasks { get; } = new();
    public ObservableCollection<UserTask> InProgressTasks { get; } = new();
    public ObservableCollection<UserTask> DoneTasks { get; } = new();

    public string NewTaskDescription
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public string NewTaskContext
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public bool ShowAddTaskInput
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool ShowEditTaskInput
    {
        get;
        set => SetProperty(ref field, value);
    }

    public string? EditingTaskId
    {
        get;
        set => SetProperty(ref field, value);
    }

    public string EditTaskDescription
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public string EditTaskContext
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public string CurrentProcessName
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public string CurrentWindowTitle
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public bool IsMonitoring
    {
        get;
        set => SetProperty(ref field, value);
    }

    public int FocusScore
    {
        get;
        set => SetProperty(ref field, value);
    }

    public string FocusReason
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public bool IsClassifying
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(IsFocusResultVisible));
            }
        }
    }

    public bool IsFocusScoreVisible => IsMonitoring;

    public bool IsFocusResultVisible => true;

    public string FocusScoreCategory =>
        FocusScore >= 6 ? "Focused"
        : FocusScore >= 4 ? "Unclear"
        : "Distracted";

    public string FocusStatusIcon =>
        FocusScore switch
        {
            >= 6 => "ms-appx:///Assets/icon-focused.svg",
            >= 4 => "ms-appx:///Assets/icon-unclear.svg",
            _ => "ms-appx:///Assets/icon-distracted.svg",
        };

    public string FocusAccentBrushKey =>
        FocusScore switch
        {
            >= 6 => "FbAlignedAccentBrush",
            >= 4 => "FbNeutralAccentBrush",
            _ => "FbMisalignedAccentBrush",
        };

    public int CurrentFocusScorePercent
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public bool IsFocusScorePercentVisible => IsMonitoring && _focusScoreService.HasRealScore;

    public string TaskElapsedTime
    {
        get;
        set => SetProperty(ref field, value);
    } = "00:00:00";

    private long _windowElapsedSeconds;

    public string WindowElapsedTime
    {
        get;
        set => SetProperty(ref field, value);
    } = "00:00:00";

    private readonly Dictionary<string, long> _perWindowTotalSeconds = new();

    public string WindowTotalElapsedTime
    {
        get;
        set => SetProperty(ref field, value);
    } = "00:00:00";

    public KanbanBoardViewModel(
        ITaskRepository repo,
        IWindowMonitorService windowMonitor,
        ITimeTrackingService timeTracking,
        IIdleDetectionService idleDetection,
        INavigationService navigationService,
        ILlmService llmService,
        ISettingsService settingsService,
        IFocusScoreService focusScoreService
    )
    {
        _repo = repo;
        _windowMonitor = windowMonitor;
        _timeTracking = timeTracking;
        _idleDetection = idleDetection;
        _navigationService = navigationService;
        _llmService = llmService;
        _settingsService = settingsService;
        _focusScoreService = focusScoreService;
        _windowMonitor.ForegroundWindowChanged += OnForegroundWindowChanged;
        _timeTracking.Tick += OnTimeTrackingTick;
        _idleDetection.UserBecameIdle += OnUserBecameIdle;
        _idleDetection.UserBecameActive += OnUserBecameActive;
        _ = LoadBoardAsync();
    }

    private static string GetFocusBotProcessName()
    {
        try
        {
            return Process.GetCurrentProcess().ProcessName ?? "FocusBot.App";
        }
        catch
        {
            return "FocusBot.App";
        }
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
        _windowElapsedSeconds++;
        WindowElapsedTime = FormatElapsed(_windowElapsedSeconds);
        var windowKey = GetCurrentWindowKey();
        if (!string.IsNullOrEmpty(windowKey))
        {
            var total = _perWindowTotalSeconds.GetValueOrDefault(windowKey, 0) + 1;
            _perWindowTotalSeconds[windowKey] = total;
            WindowTotalElapsedTime = FormatElapsed(total);
        }
        var taskId = InProgressTasks[0].TaskId;
        CurrentFocusScorePercent = _focusScoreService.CalculateFocusScorePercent(taskId);
        OnPropertyChanged(nameof(IsFocusScorePercentVisible));
        _secondsSinceLastPersist++;
        if (_secondsSinceLastPersist >= PersistIntervalSeconds)
        {
            _secondsSinceLastPersist = 0;
            _ = PersistElapsedTimeAsync(taskId);
            _ = _focusScoreService.PersistSegmentsAsync();
        }
    }

    private void OnUserBecameIdle(object? sender, EventArgs e)
    {
        if (InProgressTasks.Count == 0)
            return;

        _focusScoreService.PauseCurrentSegment();
        _timeTracking.Stop();
        _windowMonitor.Stop();
    }

    private void OnUserBecameActive(object? sender, EventArgs e)
    {
        if (InProgressTasks.Count == 0)
            return;

        _timeTracking.Start();
        _windowMonitor.Start();
    }

    private static string GetCurrentWindowKey(string processName, string windowTitle) =>
        $"{processName ?? string.Empty}|{windowTitle ?? string.Empty}";

    private string GetCurrentWindowKey() =>
        GetCurrentWindowKey(CurrentProcessName, CurrentWindowTitle);

    private async Task PersistElapsedTimeAsync(string taskId)
    {
        await _repo.UpdateElapsedTimeAsync(taskId, _taskElapsedSeconds);
    }

    private void OnForegroundWindowChanged(object? sender, ForegroundWindowChangedEventArgs e)
    {
        if (!string.IsNullOrEmpty(CurrentProcessName) && _windowElapsedSeconds > 0)
        {
            var previousKey = GetCurrentWindowKey(CurrentProcessName, CurrentWindowTitle);
            var previousTotal =
                _perWindowTotalSeconds.GetValueOrDefault(previousKey, 0) + _windowElapsedSeconds;
            _perWindowTotalSeconds[previousKey] = previousTotal;
        }

        _focusScoreService.PauseCurrentSegment();

        CurrentProcessName = e.ProcessName;
        CurrentWindowTitle = e.WindowTitle;
        _windowElapsedSeconds = 0;
        WindowElapsedTime = FormatElapsed(0);
        var newKey = GetCurrentWindowKey(e.ProcessName, e.WindowTitle);
        var newTotal = _perWindowTotalSeconds.GetValueOrDefault(newKey, 0);
        WindowTotalElapsedTime = FormatElapsed(newTotal);

        if (InProgressTasks.Count == 0)
        {
            FocusScore = 0;
            FocusReason = string.Empty;
            IsClassifying = false;
            OnPropertyChanged(nameof(IsFocusScoreVisible));
            OnPropertyChanged(nameof(IsFocusResultVisible));
            OnPropertyChanged(nameof(FocusScoreCategory));
            OnPropertyChanged(nameof(FocusStatusIcon));
            OnPropertyChanged(nameof(FocusAccentBrushKey));
            OnPropertyChanged(nameof(IsFocusScorePercentVisible));
            return;
        }

        var task = InProgressTasks[0];
        var isViewingFocusBot = string.Equals(
            e.ProcessName,
            FocusBotProcessName,
            StringComparison.OrdinalIgnoreCase
        );

        if (isViewingFocusBot)
        {
            FocusScore = 4;
            FocusReason = "Viewing FocusBot";
            IsClassifying = false;
            OnPropertyChanged(nameof(IsFocusScoreVisible));
            OnPropertyChanged(nameof(IsFocusResultVisible));
            OnPropertyChanged(nameof(FocusScoreCategory));
            OnPropertyChanged(nameof(FocusStatusIcon));
            OnPropertyChanged(nameof(FocusAccentBrushKey));
            OnPropertyChanged(nameof(IsFocusScorePercentVisible));
            return;
        }

        FocusScore = 0;
        FocusReason = string.Empty;
        IsClassifying = false;
        OnPropertyChanged(nameof(IsFocusScoreVisible));
        OnPropertyChanged(nameof(IsFocusResultVisible));
        OnPropertyChanged(nameof(FocusScoreCategory));
        OnPropertyChanged(nameof(FocusStatusIcon));
        OnPropertyChanged(nameof(FocusAccentBrushKey));
        OnPropertyChanged(nameof(IsFocusScorePercentVisible));

        var contextHash = HashHelper.ComputeWindowContextHash(e.ProcessName, e.WindowTitle);
        _focusScoreService.StartPendingSegment(
            task.TaskId,
            contextHash,
            e.WindowTitle,
            e.ProcessName
        );
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
        IsClassifying = true;
        try
        {
            var result = await _llmService.ClassifyAlignmentAsync(
                taskDescription,
                taskContext,
                processName,
                windowTitle
            );
            if (result != null)
            {
                FocusScore = result.Score;
                FocusReason = result.Reason;
                _focusScoreService.UpdatePendingSegmentScore(result.Score);
            }
        }
        finally
        {
            IsClassifying = false;
            OnPropertyChanged(nameof(IsFocusScoreVisible));
            OnPropertyChanged(nameof(IsFocusResultVisible));
            OnPropertyChanged(nameof(FocusScoreCategory));
            OnPropertyChanged(nameof(FocusStatusIcon));
            OnPropertyChanged(nameof(FocusAccentBrushKey));
            OnPropertyChanged(nameof(IsFocusScorePercentVisible));
        }
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
            var task = InProgressTasks[0];
            _taskElapsedSeconds = task.TotalElapsedSeconds;
            TaskElapsedTime = FormatElapsed(_taskElapsedSeconds);
            _windowElapsedSeconds = 0;
            WindowElapsedTime = FormatElapsed(0);
            _perWindowTotalSeconds.Clear();
            WindowTotalElapsedTime = FormatElapsed(0);
            _secondsSinceLastPersist = 0;
            await _focusScoreService.LoadSegmentsForTaskAsync(task.TaskId);
            CurrentFocusScorePercent = _focusScoreService.CalculateFocusScorePercent(task.TaskId);
            OnPropertyChanged(nameof(IsFocusScorePercentVisible));
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

    [RelayCommand]
    private void ViewTaskDetail(string? taskId)
    {
        if (string.IsNullOrEmpty(taskId))
            return;
        _navigationService.NavigateToTaskDetail(taskId);
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task AddTaskAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTaskDescription))
            return;
        var context = string.IsNullOrWhiteSpace(NewTaskContext) ? null : NewTaskContext.Trim();
        var task = await _repo.AddTaskAsync(NewTaskDescription.Trim(), context);
        ToDoTasks.Add(task);
        CloseNewTaskPopup();
    }

    private void CloseNewTaskPopup()
    {
        NewTaskDescription = string.Empty;
        NewTaskContext = string.Empty;
        ShowAddTaskInput = false;
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task BeginEditTaskAsync(string? taskId)
    {
        if (string.IsNullOrEmpty(taskId))
            return;
        var task = await _repo.GetByIdAsync(taskId);
        if (task == null)
            return;
        EditingTaskId = taskId;
        EditTaskDescription = task.Description;
        EditTaskContext = task.Context ?? string.Empty;
        ShowEditTaskInput = true;
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task SaveEditTaskAsync()
    {
        if (string.IsNullOrWhiteSpace(EditTaskDescription) || string.IsNullOrEmpty(EditingTaskId))
            return;
        var context = string.IsNullOrWhiteSpace(EditTaskContext) ? null : EditTaskContext.Trim();
        await _repo.UpdateTaskAsync(EditingTaskId, EditTaskDescription.Trim(), context);
        CloseEditTaskPopup();
        await LoadBoardAsync();
    }

    [RelayCommand]
    private void CloseEditTask() => ShowEditTaskInput = false;

    private void CloseEditTaskPopup()
    {
        EditTaskDescription = string.Empty;
        EditTaskContext = string.Empty;
        EditingTaskId = null;
        ShowEditTaskInput = false;
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task MoveToInProgressAsync(string? taskId)
    {
        if (string.IsNullOrEmpty(taskId))
            return;
        await _repo.SetStatusToAsync(taskId, TaskStatus.InProgress);
        await LoadBoardAsync();
    }

    private async Task FinalizeFocusScoreAndPersistAsync(string taskId)
    {
        _focusScoreService.PauseCurrentSegment();
        await _focusScoreService.PersistSegmentsAsync();
        var scorePercent = _focusScoreService.CalculateFocusScorePercent(taskId);
        await _repo.UpdateFocusScoreAsync(taskId, scorePercent);
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task MoveToDoneAsync(string? taskId)
    {
        if (string.IsNullOrEmpty(taskId))
            return;
        await FinalizeFocusScoreAndPersistAsync(taskId);
        await UpdateTaskStatusAndDuration(taskId, TaskStatus.Done);
        await LoadBoardAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task MoveToToDoAsync(string? taskId)
    {
        if (string.IsNullOrEmpty(taskId))
            return;
        await FinalizeFocusScoreAndPersistAsync(taskId);
        await _repo.UpdateElapsedTimeAsync(taskId, _taskElapsedSeconds);
        await _repo.SetStatusToAsync(taskId, TaskStatus.ToDo);
        await LoadBoardAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task DeleteTaskAsync(string? taskId)
    {
        if (string.IsNullOrEmpty(taskId))
            return;
        _focusScoreService.ClearTaskSegments(taskId);
        await _repo.DeleteFocusSegmentsForTaskAsync(taskId);
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
        if (HasActiveTask() && InProgressTasks[0].TaskId == taskId)
            await FinalizeFocusScoreAndPersistAsync(taskId);
        await _repo.UpdateElapsedTimeAsync(taskId, _taskElapsedSeconds);
        await _repo.SetStatusToAsync(taskId, statusEnum);
    }

    private bool HasValidFocusData() => FocusScore > 0 || !string.IsNullOrEmpty(FocusReason);

    private bool HasActiveTask() => InProgressTasks.Count > 0;

    private void StartMonitoring()
    {
        _windowMonitor.Start();
        _timeTracking.Start();
        _idleDetection.Start();
    }

    private void StopMonitoringAndResetFocusState()
    {
        _windowMonitor.Stop();
        _timeTracking.Stop();
        _idleDetection.Stop();
        _taskElapsedSeconds = 0;
        TaskElapsedTime = FormatElapsed(0);
        _windowElapsedSeconds = 0;
        WindowElapsedTime = FormatElapsed(0);
        _perWindowTotalSeconds.Clear();
        WindowTotalElapsedTime = FormatElapsed(0);
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

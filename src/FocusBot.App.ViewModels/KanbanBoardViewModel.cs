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
    private readonly INavigationService _navigationService;

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

    public KanbanBoardViewModel(ITaskRepository repo, IWindowMonitorService windowMonitor, INavigationService navigationService)
    {
        _repo = repo;
        _windowMonitor = windowMonitor;
        _navigationService = navigationService;
        _windowMonitor.ForegroundWindowChanged += OnForegroundWindowChanged;
        _ = LoadBoardAsync();
    }

    private void OnForegroundWindowChanged(object? sender, ForegroundWindowChangedEventArgs e)
    {
        CurrentProcessName = e.ProcessName;
        CurrentWindowTitle = e.WindowTitle;
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

        if (InProgressTasks.Count > 0)
            _windowMonitor.Start();
        else
        {
            _windowMonitor.Stop();
            CurrentProcessName = string.Empty;
            CurrentWindowTitle = string.Empty;
        }
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
        await _repo.SetStatusToAsync(taskId, TaskStatus.Done);
        await LoadBoardAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task MoveToToDoAsync(string? taskId)
    {
        if (string.IsNullOrEmpty(taskId))
            return;
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
        var statusEnum = Enum.Parse<TaskStatus>(status);
        await _repo.SetStatusToAsync(taskId, statusEnum);
        await LoadBoardAsync();
    }
}

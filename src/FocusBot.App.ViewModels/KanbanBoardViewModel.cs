using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusBot.Core.Configuration;
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
    private readonly ITrialService _trialService;

    private const string HasSeenHowItWorksGuideKey = "HasSeenHowItWorksGuide";

    private static readonly string FocusBotProcessName = GetFocusBotProcessName();

    /// <summary>
    /// Raised when the user requests to open the How it works guide (e.g. Help button). The view shows the dialog.
    /// </summary>
    public event EventHandler? ShowHowItWorksRequested;

    /// <summary>
    /// Raised when focus overlay state changes (score, status, or active task).
    /// </summary>
    public event EventHandler<FocusOverlayStateChangedEventArgs>? FocusOverlayStateChanged;

    /// <summary>
    /// Raised when the trial expires while the app is running. The view shows an expiration dialog.
    /// </summary>
    public event EventHandler? TrialExpired;

    private long _taskElapsedSeconds;
    private int _secondsSinceLastPersist;
    private const int PersistIntervalSeconds = 5;
    private bool _isTaskPaused;

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
                OnPropertyChanged(nameof(ShowCheckingMessage));
            }
        }
    }

    /// <summary>
    /// Gets whether the current task is paused (time tracking and monitoring stopped).
    /// </summary>
    public bool IsTaskPaused => _isTaskPaused;

    public bool IsFocusScoreVisible => IsMonitoring && IsAiConfigured;

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

    public bool IsFocusScorePercentVisible => IsMonitoring && _focusScoreService.HasRealScore && IsAiConfigured;

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

    private string _aiProviderDisplay = string.Empty;
    public string AiProviderDisplay
    {
        get => _aiProviderDisplay;
        set => SetProperty(ref _aiProviderDisplay, value);
    }

    private string _aiModelDisplay = string.Empty;
    public string AiModelDisplay
    {
        get => _aiModelDisplay;
        set => SetProperty(ref _aiModelDisplay, value);
    }

    private string _aiRequestError = string.Empty;
    public string AiRequestError
    {
        get => _aiRequestError;
        set
        {
            if (SetProperty(ref _aiRequestError, value))
            {
                OnPropertyChanged(nameof(HasAiRequestError));
                OnPropertyChanged(nameof(IsAiStatusOk));
            }
        }
    }

    public bool HasAiRequestError => !string.IsNullOrEmpty(_aiRequestError);

    public bool IsAiStatusOk => !HasAiRequestError;

    public string AiProviderAndModelDisplay =>
        string.IsNullOrEmpty(AiModelDisplay) ? AiProviderDisplay : $"{AiProviderDisplay} · {AiModelDisplay}";

    private bool _isAiConfigured;
    public bool IsAiConfigured
    {
        get => _isAiConfigured;
        private set => SetProperty(ref _isAiConfigured, value);
    }

    private bool _isTrialActive;
    public bool IsTrialActive
    {
        get => _isTrialActive;
        private set => SetProperty(ref _isTrialActive, value);
    }

    private bool _isTrialExpired;
    public bool IsTrialExpired
    {
        get => _isTrialExpired;
        private set => SetProperty(ref _isTrialExpired, value);
    }

    private DateTime? _trialEndTime;
    public DateTime? TrialEndTime
    {
        get => _trialEndTime;
        private set => SetProperty(ref _trialEndTime, value);
    }

    private string _trialTimeRemainingFormatted = string.Empty;
    public string TrialTimeRemainingFormatted
    {
        get => _trialTimeRemainingFormatted;
        private set => SetProperty(ref _trialTimeRemainingFormatted, value);
    }

    public bool ShowTrialBanner => IsTrialActive && !IsTrialExpired;

    private bool _hasCurrentFocusResult;
    public bool HasCurrentFocusResult
    {
        get => _hasCurrentFocusResult;
        private set
        {
            if (SetProperty(ref _hasCurrentFocusResult, value))
                OnPropertyChanged(nameof(ShowCheckingMessage));
        }
    }

    public bool ShowCheckingMessage => !HasCurrentFocusResult && IsClassifying;

    public KanbanBoardViewModel(
        ITaskRepository repo,
        IWindowMonitorService windowMonitor,
        ITimeTrackingService timeTracking,
        IIdleDetectionService idleDetection,
        INavigationService navigationService,
        ILlmService llmService,
        ISettingsService settingsService,
        IFocusScoreService focusScoreService,
        ITrialService trialService
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
        _trialService = trialService;
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
        RaiseFocusOverlayStateChanged();
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
            HasCurrentFocusResult = true;
            OnPropertyChanged(nameof(IsFocusScoreVisible));
            OnPropertyChanged(nameof(IsFocusResultVisible));
            OnPropertyChanged(nameof(FocusScoreCategory));
            OnPropertyChanged(nameof(FocusStatusIcon));
            OnPropertyChanged(nameof(FocusAccentBrushKey));
            OnPropertyChanged(nameof(IsFocusScorePercentVisible));
            OnPropertyChanged(nameof(ShowCheckingMessage));
            return;
        }

        FocusScore = 0;
        FocusReason = string.Empty;
        IsClassifying = false;
        HasCurrentFocusResult = false;
        OnPropertyChanged(nameof(IsFocusScoreVisible));
        OnPropertyChanged(nameof(IsFocusResultVisible));
        OnPropertyChanged(nameof(FocusScoreCategory));
        OnPropertyChanged(nameof(FocusStatusIcon));
        OnPropertyChanged(nameof(FocusAccentBrushKey));
        OnPropertyChanged(nameof(IsFocusScorePercentVisible));
        OnPropertyChanged(nameof(ShowCheckingMessage));

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
        AiRequestError = string.Empty;
        try
        {
            var response = await _llmService.ClassifyAlignmentAsync(
                taskDescription,
                taskContext,
                processName,
                windowTitle
            );
            if (response.ErrorMessage != null)
                AiRequestError = response.ErrorMessage;
            if (response.Result != null)
            {
                FocusScore = response.Result.Score;
                FocusReason = response.Result.Reason;
                _focusScoreService.UpdatePendingSegmentScore(response.Result.Score);
                AiRequestError = string.Empty;
                HasCurrentFocusResult = true;
                RaiseFocusOverlayStateChanged();
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
            OnPropertyChanged(nameof(ShowCheckingMessage));
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
        await RefreshAiSettingsAsync();
    }

    /// <summary>
    /// Toggles the task pause state. When paused, time tracking, window monitoring, and classification are stopped.
    /// </summary>
    public void ToggleTaskPause()
    {
        if (InProgressTasks.Count == 0)
            return;

        _isTaskPaused = !_isTaskPaused;
        OnPropertyChanged(nameof(IsTaskPaused));

        if (_isTaskPaused)
        {
            _focusScoreService.PauseCurrentSegment();
            _timeTracking.Stop();
            _windowMonitor.Stop();
        }
        else
        {
            _timeTracking.Start();
            _windowMonitor.Start();
        }

        RaiseFocusOverlayStateChanged();
    }

    /// <summary>
    /// Refreshes the displayed AI provider and model from settings. Call when returning to the board so the corner label is up to date.
    /// </summary>
    public async Task RefreshAiSettingsAsync()
    {
        var providerId = await _settingsService.GetProviderAsync()
            ?? LlmProviderConfig.DefaultProvider.ProviderId;
        var modelId = await _settingsService.GetModelAsync();
        var provider = LlmProviderConfig.Providers.FirstOrDefault(p => p.ProviderId == providerId)
            ?? LlmProviderConfig.DefaultProvider;
        AiProviderDisplay = provider.DisplayName;
        if (string.IsNullOrEmpty(modelId))
        {
            AiModelDisplay = LlmProviderConfig.Models.TryGetValue(providerId, out var models) && models.Count > 0
                ? models[0].DisplayName
                : string.Empty;
        }
        else
        {
            AiModelDisplay = LlmProviderConfig.Models.TryGetValue(providerId, out var models)
                ? models.FirstOrDefault(m => m.ModelId == modelId)?.DisplayName ?? modelId
                : modelId;
        }
        OnPropertyChanged(nameof(AiProviderAndModelDisplay));
        IsAiConfigured = await _llmService.IsConfiguredAsync();
        if (IsAiConfigured)
            AiRequestError = string.Empty;
        OnPropertyChanged(nameof(IsFocusScoreVisible));
        OnPropertyChanged(nameof(IsFocusScorePercentVisible));
    }

    /// <summary>
    /// Starts the 24-hour free trial if not already started.
    /// </summary>
    public async Task StartTrialAsync()
    {
        await _trialService.StartTrialAsync();
        await RefreshTrialStateAsync();
        await RefreshAiSettingsAsync();
    }

    /// <summary>
    /// Refreshes the trial state properties from the trial service.
    /// </summary>
    public async Task RefreshTrialStateAsync()
    {
        // Hide trial UI if user has configured their own API key or has an active subscription
        var mode = await _settingsService.GetApiKeyModeAsync();
        if (mode == ApiKeyMode.Own)
        {
            var ownKey = await _settingsService.GetApiKeyAsync();
            if (!string.IsNullOrWhiteSpace(ownKey))
            {
                IsTrialActive = false;
                IsTrialExpired = false;
                TrialEndTime = null;
                TrialTimeRemainingFormatted = string.Empty;
                OnPropertyChanged(nameof(ShowTrialBanner));
                return;
            }
        }

        // Check actual trial status
        IsTrialActive = await _trialService.IsTrialActiveAsync();
        IsTrialExpired = await _trialService.IsTrialExpiredAsync();
        TrialEndTime = await _trialService.GetTrialEndTimeAsync();
        await UpdateTrialTimeRemainingAsync();
        OnPropertyChanged(nameof(ShowTrialBanner));
    }

    /// <summary>
    /// Updates the trial countdown display. Called every second by the view's timer.
    /// </summary>
    public async Task UpdateTrialTimeRemainingAsync()
    {
        var remaining = await _trialService.GetTrialTimeRemainingAsync();
        if (remaining <= TimeSpan.Zero)
        {
            if (IsTrialActive && !IsTrialExpired)
            {
                // Trial just expired - raise event for view to show popup
                IsTrialActive = false;
                IsTrialExpired = true;
                OnPropertyChanged(nameof(ShowTrialBanner));
                TrialExpired?.Invoke(this, EventArgs.Empty);
                await RefreshAiSettingsAsync();
            }
            TrialTimeRemainingFormatted = "Expired";
            return;
        }

        var hours = (int)remaining.TotalHours;
        var minutes = remaining.Minutes;
        var seconds = remaining.Seconds;
        TrialTimeRemainingFormatted = $"{hours:D2}:{minutes:D2}:{seconds:D2}";
    }

    /// <summary>
    /// Gets whether the trial has already been started.
    /// </summary>
    public Task<bool> HasTrialStartedAsync() => _trialService.HasTrialStartedAsync();

    [RelayCommand]
    private void ToggleAddTask() => ShowAddTaskInput = !ShowAddTaskInput;

    [RelayCommand]
    private void OpenSettings() => _navigationService.NavigateToSettings();

    [RelayCommand]
    private void OpenHowItWorks() => ShowHowItWorksRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Returns true if the user has not yet seen the How it works guide (first run).
    /// </summary>
    public async Task<bool> GetHasSeenHowItWorksGuideAsync()
    {
        var value = await _settingsService.GetSettingAsync<bool>(HasSeenHowItWorksGuideKey);
        return value == true;
    }

    /// <summary>
    /// Marks the How it works guide as seen so it is not shown automatically again.
    /// </summary>
    public Task SetHasSeenHowItWorksGuideAsync() =>
        _settingsService.SetSettingAsync(HasSeenHowItWorksGuideKey, true);

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
        _isTaskPaused = false;
        OnPropertyChanged(nameof(IsTaskPaused));
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
        HasCurrentFocusResult = false;
        OnPropertyChanged(nameof(IsFocusScoreVisible));
        RaiseFocusOverlayStateChanged();
    }

    private void RaiseFocusOverlayStateChanged()
    {
        var hasActive = HasActiveTask();
        var status = FocusScore switch
        {
            >= 6 => FocusStatus.Focused,
            >= 4 => FocusStatus.Neutral,
            _ => FocusStatus.Distracted
        };
        FocusOverlayStateChanged?.Invoke(this, new FocusOverlayStateChangedEventArgs
        {
            HasActiveTask = hasActive,
            FocusScorePercent = hasActive ? CurrentFocusScorePercent : 0,
            Status = status,
            IsTaskPaused = _isTaskPaused
        });
    }
}

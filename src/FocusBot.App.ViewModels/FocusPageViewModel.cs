using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusBot.Core;
using FocusBot.Core.Configuration;
using FocusBot.Core.DTOs;
using FocusBot.Core.Entities;
using FocusBot.Core.Events;
using FocusBot.Core.Helpers;
using FocusBot.Core.Interfaces;

namespace FocusBot.App.ViewModels;

public partial class FocusPageViewModel : ObservableObject
{
    private readonly ITaskRepository _repo;
    private readonly IWindowMonitorService _windowMonitor;
    private readonly INavigationService _navigationService;
    private readonly IClassificationService _classificationService;
    private readonly ISettingsService _settingsService;
    private readonly ILocalSessionTracker _sessionTracker;
    private readonly IAlignmentCacheRepository _alignmentCacheRepository;
    private readonly IFocusBotApiClient _apiClient;
    private readonly IAuthService? _authService;
    private readonly IDeviceService? _deviceService;
    private Guid? _backendSessionId;
    public AccountSettingsViewModel AccountSection { get; }
    private readonly IIntegrationService? _integrationService;
    private readonly IUIThreadDispatcher? _uiDispatcher;

    private static readonly string FocusBotProcessName = GetFocusBotProcessName();

    /// <summary>
    /// Raised when the user requests to open the How it works guide (e.g. Help button). The view shows the dialog.
    /// </summary>
    public event EventHandler? ShowHowItWorksRequested;

    /// <summary>
    /// Raised when focus overlay state changes (score, status, or active task).
    /// </summary>
    public event EventHandler<FocusOverlayStateChangedEventArgs>? FocusOverlayStateChanged;

    private long _taskElapsedSeconds;
    private int _secondsSinceLastPersist;
    private bool _isTaskPaused;
    private DateTime? _sessionStartUtc;

    // Single local in-progress task
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowStartForm))]
    [NotifyPropertyChangedFor(nameof(IsActiveTaskVisible))]
    private UserTask? _activeTask;

    [ObservableProperty]
    private string _startTaskTitle = string.Empty;

    [ObservableProperty]
    private string _startTaskContext = string.Empty;

    public bool ShowStartForm => ActiveTask == null;
    public bool IsActiveTaskVisible => ActiveTask != null;

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

    public bool IsFocusResultVisible => ActiveTask != null;

    public string FocusScoreCategory =>
        FocusScore >= 6 ? "Focused"
        : FocusScore >= 4 ? "Unclear"
        : "Distracted";

    public string FocusStatusIcon =>
        (IsMonitoring && !HasCurrentFocusResult)
            ? "ms-appx:///Assets/icon-unclear.svg"
            : FocusScore switch
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

    public bool IsFocusScorePercentVisible =>
        IsMonitoring && IsAiConfigured;

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

    public string AiProviderDisplay
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public string AiModelDisplay
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

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
        string.IsNullOrEmpty(AiModelDisplay)
            ? AiProviderDisplay
            : $"{AiProviderDisplay} · {AiModelDisplay}";

    public bool IsAiConfigured
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public bool HasCurrentFocusResult
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
                OnPropertyChanged(nameof(ShowCheckingMessage));
        }
    }

    public bool ShowCheckingMessage => IsMonitoring && !HasCurrentFocusResult;

    public bool ShowMarkOverrideButton =>
        HasCurrentFocusResult && !IsClassifying && !IsFocusBotWindow;

    public string MarkOverrideButtonText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "Mark as distracting";

    public bool IsFocusBotWindow
    {
        get
        {
            return string.Equals(
                CurrentProcessName,
                FocusBotProcessName,
                StringComparison.OrdinalIgnoreCase
            );
        }
    }



    public bool IsExtensionConnected
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
                OnPropertyChanged(nameof(ShowExtensionPromo));
        }
    }

    /// <summary>
    /// True when the foreground window is Microsoft Edge or Google Chrome (used to show extension promo only for supported browsers).
    /// </summary>
    public bool IsForegroundBrowserEdgeOrChrome =>
        BrowserProcessNames.IsExtensionSupported(CurrentProcessName);

    /// <summary>
    /// True when we should show the "install extension" promo: extension not connected and foreground app is Edge or Chrome.
    /// </summary>
    public bool ShowExtensionPromo => !IsExtensionConnected && IsForegroundBrowserEdgeOrChrome;

    /// <summary>
    /// Microsoft Edge Add-ons store URL for the FocusBot extension.
    /// </summary>
    public Uri ExtensionStoreEdgeUri => ExtensionStoreLinks.EdgeAddOns;

    /// <summary>
    /// Chrome Web Store URL for the FocusBot extension.
    /// </summary>
    public Uri ExtensionStoreChromeUri => ExtensionStoreLinks.ChromeWebStore;

    public FocusPageViewModel(
        ITaskRepository repo,
        IWindowMonitorService windowMonitor,
        INavigationService navigationService,
        IClassificationService classificationService,
        ISettingsService settingsService,
        ILocalSessionTracker sessionTracker,
        IAlignmentCacheRepository alignmentCacheRepository,
        IFocusBotApiClient apiClient,
        AccountSettingsViewModel accountSection,
        IIntegrationService? integrationService = null,
        IUIThreadDispatcher? uiDispatcher = null,
        IDeviceService? deviceService = null,
        IAuthService? authService = null
    )
    {
        _repo = repo;
        _windowMonitor = windowMonitor;
        _navigationService = navigationService;
        _classificationService = classificationService;
        _settingsService = settingsService;
        _sessionTracker = sessionTracker;
        _alignmentCacheRepository = alignmentCacheRepository;
        _apiClient = apiClient;
        AccountSection = accountSection;
        _integrationService = integrationService;
        _uiDispatcher = uiDispatcher;
        _deviceService = deviceService;
        _authService = authService;
        _windowMonitor.ForegroundWindowChanged += OnForegroundWindowChanged;
        _windowMonitor.Tick += OnTimeTrackingTick;
        _windowMonitor.UserBecameIdle += OnUserBecameIdle;
        _windowMonitor.UserBecameActive += OnUserBecameActive;

        if (_authService != null)
        {
            _authService.AuthStateChanged += OnAuthStateChanged;
        }

        if (_integrationService != null)
        {
            _integrationService.ExtensionConnectionChanged += OnExtensionConnectionChanged;
        }

        _ = LoadBoardAsync();
    }

    private void OnAuthStateChanged()
    {
        if (_uiDispatcher != null)
        {
            _ = _uiDispatcher.RunOnUIThreadAsync(async () => await RefreshAiSettingsAsync());
            return;
        }

        _ = RefreshAiSettingsAsync();
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



    private void OnTimeTrackingTick(object? sender, EventArgs e)
    {
        if (ActiveTask == null)
            return;

        _taskElapsedSeconds++;
        TaskElapsedTime = TimeFormatHelper.FormatElapsed(_taskElapsedSeconds);
        _windowElapsedSeconds++;
        WindowElapsedTime = TimeFormatHelper.FormatElapsed(_windowElapsedSeconds);
        var windowKey = GetCurrentWindowKey();
        if (!string.IsNullOrEmpty(windowKey))
        {
            var total = _perWindowTotalSeconds.GetValueOrDefault(windowKey, 0) + 1;
            _perWindowTotalSeconds[windowKey] = total;
            WindowTotalElapsedTime = TimeFormatHelper.FormatElapsed(total);
        }
        var taskId = ActiveTask.TaskId;
        _sessionTracker.RecordTick();
        CurrentFocusScorePercent = _sessionTracker.GetFocusScore();
        OnPropertyChanged(nameof(IsFocusScorePercentVisible));
        RaiseFocusOverlayStateChanged();
        _secondsSinceLastPersist++;
        if (_secondsSinceLastPersist >= FocusSessionConfig.PersistIntervalSeconds)
        {
            _secondsSinceLastPersist = 0;
            _ = PersistElapsedTimeAsync(taskId);
        }
    }

    private void OnUserBecameIdle(object? sender, EventArgs e)
    {
        if (ActiveTask == null)
            return;

        var backdateSeconds = (int)_windowMonitor.IdleThreshold.TotalSeconds;
        _taskElapsedSeconds = Math.Max(0L, _taskElapsedSeconds - backdateSeconds);
        _windowElapsedSeconds = Math.Max(0L, _windowElapsedSeconds - backdateSeconds);
        var key = GetCurrentWindowKey();
        if (!string.IsNullOrEmpty(key))
        {
            var current = _perWindowTotalSeconds.GetValueOrDefault(key, 0L);
            _perWindowTotalSeconds[key] = Math.Max(0L, current - backdateSeconds);
        }

        TaskElapsedTime = TimeFormatHelper.FormatElapsed(_taskElapsedSeconds);
        WindowElapsedTime = TimeFormatHelper.FormatElapsed(_windowElapsedSeconds);
        WindowTotalElapsedTime = TimeFormatHelper.FormatElapsed(
            _perWindowTotalSeconds.GetValueOrDefault(key ?? string.Empty, 0L)
        );

        _sessionTracker.HandleIdle(true);
        _windowMonitor.Stop();
    }

    private void OnUserBecameActive(object? sender, EventArgs e)
    {
        if (ActiveTask == null)
            return;

        _sessionTracker.HandleIdle(false);
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

        CurrentProcessName = e.ProcessName;
        CurrentWindowTitle = e.WindowTitle;
        OnPropertyChanged(nameof(IsForegroundBrowserEdgeOrChrome));
        OnPropertyChanged(nameof(ShowExtensionPromo));
        _windowElapsedSeconds = 0;
        WindowElapsedTime = TimeFormatHelper.FormatElapsed(0);
        var newKey = GetCurrentWindowKey(e.ProcessName, e.WindowTitle);
        var newTotal = _perWindowTotalSeconds.GetValueOrDefault(newKey, 0);
        WindowTotalElapsedTime = TimeFormatHelper.FormatElapsed(newTotal);

        if (ActiveTask == null)
        {
            if (_integrationService is { IsExtensionConnected: true })
            {
                _ = _integrationService.SendDesktopForegroundAsync(e.ProcessName, e.WindowTitle);
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
            return;
        }

        var taskDescription = ActiveTask.Description;
        var taskContext = ActiveTask.Context;
        var isViewingFocusBot = string.Equals(
            e.ProcessName,
            FocusBotProcessName,
            StringComparison.OrdinalIgnoreCase
        );

        if (isViewingFocusBot)
        {
            FocusScore = 4;
            FocusReason = "Viewing Foqus";
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

        if (_integrationService is { IsExtensionConnected: true })
            _ = _integrationService.SendDesktopForegroundAsync(e.ProcessName, e.WindowTitle);

        if (
            IsBrowserProcess(e.ProcessName) && _integrationService is { IsExtensionConnected: true }
        )
        {
            _ = ClassifyWithBrowserContextAsync(
                taskDescription,
                taskContext,
                e.ProcessName,
                e.WindowTitle
            );
        }
        else
        {
            _ = ClassifyAndUpdateFocusAsync(
                taskDescription,
                taskContext,
                e.ProcessName,
                e.WindowTitle
            );
        }
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
            var result = await _classificationService.ClassifyAsync(
                processName,
                windowTitle,
                taskDescription,
                taskContext
            );
            if (result.IsFailure)
            {
                AiRequestError = result.Error;
            }
            else
            {
                FocusScore = result.Value.Score;
                FocusReason = result.Value.Reason;
                MarkOverrideButtonText =
                    FocusScore >= 6 ? "Mark as distracting" : "Mark as focused";
                _sessionTracker.RecordClassification(processName, result.Value);
                AiRequestError = string.Empty;
                HasCurrentFocusResult = true;
                CurrentFocusScorePercent = _sessionTracker.GetFocusScore();
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
        ActiveTask = null;

        var inProgress = await _repo.GetInProgressTaskAsync();
        if (inProgress != null)
        {
            ActiveTask = inProgress;
        }

        if (HasActiveTask())
        {
            var task = ActiveTask!;
            _taskElapsedSeconds = task.TotalElapsedSeconds;
            TaskElapsedTime = TimeFormatHelper.FormatElapsed(_taskElapsedSeconds);
            _windowElapsedSeconds = 0;
            WindowElapsedTime = TimeFormatHelper.FormatElapsed(0);
            _perWindowTotalSeconds.Clear();
            WindowTotalElapsedTime = TimeFormatHelper.FormatElapsed(0);
            _secondsSinceLastPersist = 0;
            _sessionTracker.Start(task.Description);
            CurrentFocusScorePercent = 0;
            OnPropertyChanged(nameof(IsFocusScorePercentVisible));
            StartMonitoring();
        }
        else
            StopMonitoringAndResetFocusState();
        UpdateMonitoringState();
        await RefreshAiSettingsAsync();

        // Sync backend session state so EndTask can close it properly
        await SyncBackendSessionAsync();
    }

    /// <summary>
    /// Toggles the task pause state. When paused, time tracking, window monitoring, and classification are stopped.
    /// </summary>
    public void ToggleTaskPause()
    {
        if (ActiveTask == null)
            return;

        _isTaskPaused = !_isTaskPaused;
        OnPropertyChanged(nameof(IsTaskPaused));

        if (_isTaskPaused)
        {
            _sessionTracker.HandleIdle(true);
            _windowMonitor.Stop();
        }
        else
        {
            _windowMonitor.Start();
        }

        RaiseFocusOverlayStateChanged();
    }

    /// <summary>
    /// Refreshes the displayed AI provider and model from settings. Call when returning to the board so the corner label is up to date.
    /// </summary>
    public async Task RefreshAiSettingsAsync()
    {
        var providerId =
            await _settingsService.GetProviderAsync()
            ?? LlmProviderConfig.DefaultProvider.ProviderId;
        var modelId = await _settingsService.GetModelAsync();
        var provider =
            LlmProviderConfig.Providers.FirstOrDefault(p => p.ProviderId == providerId)
            ?? LlmProviderConfig.DefaultProvider;
        AiProviderDisplay = provider.DisplayName;
        if (string.IsNullOrEmpty(modelId))
        {
            AiModelDisplay =
                LlmProviderConfig.Models.TryGetValue(providerId, out var models) && models.Count > 0
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
        IsAiConfigured = _apiClient.IsConfigured;
        if (IsAiConfigured)
            AiRequestError = string.Empty;
        OnPropertyChanged(nameof(IsFocusScoreVisible));
        OnPropertyChanged(nameof(IsFocusScorePercentVisible));
    }

    [RelayCommand(CanExecute = nameof(CanStartTask))]
    private async Task StartTaskAsync()
    {
        if (string.IsNullOrWhiteSpace(StartTaskTitle))
            return;

        var context = string.IsNullOrWhiteSpace(StartTaskContext) ? null : StartTaskContext.Trim();

        // Create task directly as InProgress
        var task = await _repo.AddTaskAsync(StartTaskTitle.Trim(), context);
        await _repo.SetActiveAsync(task.TaskId);

        // Clear form
        StartTaskTitle = string.Empty;
        StartTaskContext = string.Empty;

        // Reload board which will start monitoring
        await LoadBoardAsync();

        if (ActiveTask != null)
        {
            _ = StartBackendSessionAsync(ActiveTask);
        }
    }

    private bool CanStartTask() => !string.IsNullOrWhiteSpace(StartTaskTitle);

    partial void OnStartTaskTitleChanged(string value)
    {
        StartTaskCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task EndTaskAsync()
    {
        var taskToEnd = ActiveTask;
        if (taskToEnd == null)
            return;

        var summary = _sessionTracker.GetSessionSummary();
        await _repo.UpdateFocusScoreAsync(taskToEnd.TaskId, summary.FocusScorePercent);
        await _repo.SetCompletedAsync(taskToEnd.TaskId);
        if (_backendSessionId.HasValue)
        {
            var payload = new EndSessionPayload(
                summary.FocusScorePercent,
                summary.FocusedSeconds,
                summary.DistractedSeconds,
                summary.DistractionCount,
                summary.ContextSwitchCount,
                summary.TopDistractingApps,
                summary.TopAlignedApps,
                null
            );
            _ = _apiClient.EndSessionAsync(_backendSessionId.Value, payload);
            _backendSessionId = null;
        }
        _sessionTracker.Reset();

        ActiveTask = null;

        StopMonitoringAndResetFocusState();
        UpdateMonitoringState();
    }

    [RelayCommand]
    private void PauseTask()
    {
        if (ActiveTask == null)
            return;

        _isTaskPaused = true;
        OnPropertyChanged(nameof(IsTaskPaused));

        _sessionTracker.HandleIdle(true);
        _windowMonitor.Stop();

        RaiseFocusOverlayStateChanged();
    }

    [RelayCommand]
    private void ResumeTask()
    {
        if (ActiveTask == null)
            return;

        _isTaskPaused = false;
        OnPropertyChanged(nameof(IsTaskPaused));

        _sessionTracker.HandleIdle(false);
        _windowMonitor.Start();

        RaiseFocusOverlayStateChanged();
    }

    [RelayCommand]
    private void OpenSettings() => _navigationService.NavigateToSettings();

    [RelayCommand]
    private void OpenHowItWorks() => ShowHowItWorksRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task MarkFocusOverrideAsync()
    {
        if (ActiveTask == null)
            return;

        int newScore = FocusScore >= 6 ? 2 : 9;
        string newReason =
            FocusScore >= 6 ? "Manually marked as Distracting" : "Manually marked as Focused";

        var taskId = ActiveTask.TaskId;
        var taskDescription = ActiveTask.Description;
        var taskContext = ActiveTask.Context;
        var contextHash = HashHelper.ComputeWindowContextHash(
            CurrentProcessName,
            CurrentWindowTitle
        );
        var taskContentHash = HashHelper.ComputeTaskContentHash(taskDescription, taskContext);

        var entry = new AlignmentCacheEntry
        {
            ContextHash = contextHash,
            TaskContentHash = taskContentHash,
            Score = newScore,
            Reason = newReason,
            CreatedAt = DateTime.UtcNow,
        };

        await _alignmentCacheRepository.SaveAsync(entry);

        FocusScore = newScore;
        FocusReason = newReason;
        MarkOverrideButtonText = newScore >= 6 ? "Mark as distracting" : "Mark as focused";
        _sessionTracker.RecordClassification(
            CurrentProcessName,
            new AlignmentResult { Score = newScore, Reason = newReason }
        );

        OnPropertyChanged(nameof(FocusScoreCategory));
        OnPropertyChanged(nameof(FocusStatusIcon));
        RaiseFocusOverlayStateChanged();
    }

    /// Returns true if the user has not yet seen the How it works guide (first run).
    /// </summary>
    public async Task<bool> GetHasSeenHowItWorksGuideAsync()
    {
        var value = await _settingsService.GetSettingAsync<bool>(SettingsKeys.HasSeenHowItWorksGuide);
        return value == true;
    }

    /// <summary>
    /// Marks the How it works guide as seen so it is not shown automatically again.
    /// </summary>
    public Task SetHasSeenHowItWorksGuideAsync() =>
        _settingsService.SetSettingAsync(SettingsKeys.HasSeenHowItWorksGuide, true);

    [RelayCommand]
    private void ViewTaskDetail(string? taskId)
    {
        if (string.IsNullOrEmpty(taskId))
            return;
        _navigationService.NavigateToTaskDetail(taskId);
    }

    private async Task StartBackendSessionAsync(UserTask task)
    {
        if (!_apiClient.IsConfigured)
            return;
        var deviceId = _deviceService?.GetDeviceId();
        var payload = new StartSessionPayload(task.Description, task.Context, deviceId);
        var response = await _apiClient.StartSessionAsync(payload);
        if (response is not null)
        {
            _backendSessionId = response.Id;
            return;
        }

        // StartSession failed — likely 409 Conflict (active session already exists).
        // End the stale session and retry creating a new one for the current task.
        var staleSession = await _apiClient.GetActiveSessionAsync();
        if (staleSession is null)
            return; // No active session found, nothing more we can do

        // End the stale session with empty metrics (user is abandoning it)
        var abandonPayload = new EndSessionPayload(
            FocusScorePercent: 0,
            FocusedSeconds: 0,
            DistractedSeconds: 0,
            DistractionCount: 0,
            ContextSwitchCount: 0,
            TopDistractingApps: null,
            TopAlignedApps: null,
            DeviceId: deviceId
        );
        await _apiClient.EndSessionAsync(staleSession.Id, abandonPayload);

        // Retry starting the new session
        var retryResponse = await _apiClient.StartSessionAsync(payload);
        if (retryResponse is not null)
            _backendSessionId = retryResponse.Id;
    }

    /// <summary>
    /// Syncs the backend session state on startup. If an active session exists on the backend,
    /// adopts it so that EndTask can properly close it.
    /// </summary>
    private async Task SyncBackendSessionAsync()
    {
        if (!_apiClient.IsConfigured)
            return;

        var activeSession = await _apiClient.GetActiveSessionAsync();
        if (activeSession is not null)
            _backendSessionId = activeSession.Id;
    }

    private bool HasActiveTask() => ActiveTask != null;

    private void StartMonitoring()
    {
        _windowMonitor.Start();
        if (_sessionStartUtc is null)
        {
            _sessionStartUtc = DateTime.UtcNow;
        }
    }

    private void StopMonitoringAndResetFocusState()
    {
        _windowMonitor.Stop();
        _sessionTracker.HandleIdle(false);
        _isTaskPaused = false;
        OnPropertyChanged(nameof(IsTaskPaused));
        _taskElapsedSeconds = 0;
        TaskElapsedTime = TimeFormatHelper.FormatElapsed(0);
        _windowElapsedSeconds = 0;
        WindowElapsedTime = TimeFormatHelper.FormatElapsed(0);
        _perWindowTotalSeconds.Clear();
        WindowTotalElapsedTime = TimeFormatHelper.FormatElapsed(0);
        _secondsSinceLastPersist = 0;
        _sessionStartUtc = null;
        ResetFocusState();
    }

    private void ResetFocusState()
    {
        CurrentProcessName = string.Empty;
        CurrentWindowTitle = string.Empty;
        OnPropertyChanged(nameof(IsForegroundBrowserEdgeOrChrome));
        OnPropertyChanged(nameof(ShowExtensionPromo));
        FocusScore = 0;
        FocusReason = string.Empty;
        HasCurrentFocusResult = false;
        OnPropertyChanged(nameof(IsFocusScoreVisible));
        OnPropertyChanged(nameof(FocusStatusIcon));
        RaiseFocusOverlayStateChanged();
    }

    private void RaiseFocusOverlayStateChanged()
    {
        var hasActive = HasActiveTask();
        var isLoading = hasActive && !HasCurrentFocusResult;
        var hasError = HasAiRequestError;
        var status = FocusScore switch
        {
            >= 6 => FocusStatus.Focused,
            >= 4 => FocusStatus.Neutral,
            _ => FocusStatus.Distracted,
        };
        var tooltip = BuildOverlayTooltip(hasActive, isLoading, hasError, status);
        FocusOverlayStateChanged?.Invoke(
            this,
            new FocusOverlayStateChangedEventArgs
            {
                HasActiveTask = hasActive,
                FocusScorePercent = hasActive ? CurrentFocusScorePercent : 0,
                Status = status,
                IsTaskPaused = _isTaskPaused,
                IsLoading = isLoading,
                HasError = hasError,
                TooltipText = tooltip,
            }
        );
    }

    /// <summary>Builds a concise tooltip string for the overlay tray area.</summary>
    private string BuildOverlayTooltip(
        bool hasActive,
        bool isLoading,
        bool hasError,
        FocusStatus status
    )
    {
        if (!hasActive)
            return "Foqus — No active task";

        if (_isTaskPaused)
            return "Foqus — Paused";

        if (hasError)
            return $"Foqus — Error: {_aiRequestError}";

        if (isLoading)
            return "Foqus — Classifying…";

        var label = status switch
        {
            FocusStatus.Focused => "Focused",
            FocusStatus.Distracted => "Distracted",
            _ => "Neutral",
        };

        return $"Foqus — {label} ({CurrentFocusScorePercent}%)";
    }

    private static bool IsBrowserProcess(string processName) =>
        BrowserProcessNames.IsBrowser(processName);

    private void OnExtensionConnectionChanged(object? sender, bool connected)
    {
        void UpdateConnectionState()
        {
            IsExtensionConnected = connected;
        }

        if (_uiDispatcher != null)
        {
            _ = _uiDispatcher.RunOnUIThreadAsync(() =>
            {
                UpdateConnectionState();
                return Task.CompletedTask;
            });
        }
        else
        {
            UpdateConnectionState();
        }
    }

    private void UpdateMonitoringState()
    {
        IsMonitoring = ActiveTask != null;
        OnPropertyChanged(nameof(ShowStartForm));
        OnPropertyChanged(nameof(IsActiveTaskVisible));
        OnPropertyChanged(nameof(FocusStatusIcon));
        OnPropertyChanged(nameof(IsFocusResultVisible));
        OnPropertyChanged(nameof(ShowCheckingMessage));
    }

    /// <summary>
    /// Refreshes IsExtensionConnected from the integration service. Call when the Focus page is shown
    /// so the UI reflects the current connection state even if the connection event was missed or delivered on a background thread.
    /// </summary>
    public void RefreshExtensionConnectionState()
    {
        var connected = _integrationService?.IsExtensionConnected ?? false;
        if (IsExtensionConnected != connected)
            IsExtensionConnected = connected;
    }

    /// <summary>
    /// When a browser is in the foreground and we have context from the extension, classify using both process and URL.
    /// </summary>
    private async Task ClassifyWithBrowserContextAsync(
        string taskDescription,
        string? taskContext,
        string processName,
        string windowTitle
    )
    {
        if (_integrationService is not { IsExtensionConnected: true })
        {
            await ClassifyAndUpdateFocusAsync(
                taskDescription,
                taskContext,
                processName,
                windowTitle
            );
            return;
        }

        var browserContext = _integrationService.LastBrowserContext;
        if (browserContext != null && !string.IsNullOrEmpty(browserContext.Url))
        {
            var domain =
                Uri.TryCreate(browserContext.Url, UriKind.Absolute, out var uri)
                && uri.IsAbsoluteUri
                    ? uri.Host
                    : browserContext.Url;
            var displayTitle = $"Browser: {domain}";

            if (_uiDispatcher != null)
            {
                await _uiDispatcher.RunOnUIThreadAsync(() =>
                {
                    CurrentWindowTitle = displayTitle;
                    return Task.CompletedTask;
                });
            }
            else
            {
                CurrentWindowTitle = displayTitle;
            }

            var combinedTitle = $"{browserContext.Title} ({browserContext.Url})";
            await ClassifyAndUpdateFocusAsync(
                taskDescription,
                taskContext,
                processName,
                combinedTitle
            );
        }
        else
        {
            await ClassifyAndUpdateFocusAsync(
                taskDescription,
                taskContext,
                processName,
                windowTitle
            );
        }
    }
}

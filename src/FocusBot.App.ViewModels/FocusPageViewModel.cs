using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusBot.Core;
using FocusBot.Core.Configuration;
using FocusBot.Core.DTOs;
using FocusBot.Core.Entities;
using FocusBot.Core.Events;
using FocusBot.Core.Helpers;
using FocusBot.Core.Interfaces;
using TaskStatus = FocusBot.Core.Entities.TaskStatus;

namespace FocusBot.App.ViewModels;

public partial class FocusPageViewModel : ObservableObject
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
    private readonly IDistractionDetectorService _distractionDetectorService;
    private readonly IDistractionEventRepository _distractionEventRepository;
    private readonly IDailyAnalyticsService _dailyAnalyticsService;
    private readonly IAlignmentCacheRepository _alignmentCacheRepository;
    private readonly IIntegrationService? _integrationService;
    private readonly IUIThreadDispatcher? _uiDispatcher;

    private const string HasSeenHowItWorksGuideKey = "HasSeenHowItWorksGuide";

    private static readonly string FocusBotProcessName = GetFocusBotProcessName();

    private static readonly HashSet<string> BrowserProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "brave", "opera", "vivaldi",
        "Google Chrome", "Microsoft Edge", "Firefox", "Brave Browser"
    };

    private static readonly HashSet<string> EdgeOrChromeProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "msedge", "chrome", "Microsoft Edge", "Google Chrome"
    };

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
    private DateTime? _sessionStartUtc;
    private bool _extensionHasActiveTask;

    public ObservableCollection<UserTask> ToDoTasks { get; } = new();
    public ObservableCollection<UserTask> InProgressTasks { get; } = new();
    public ObservableCollection<UserTask> DoneTasks { get; } = new();

    // New single-task properties
    [ObservableProperty]
    private UserTask? _activeTask;

    partial void OnActiveTaskChanged(UserTask? value)
    {
        OnPropertyChanged(nameof(ShowStartForm));
        OnPropertyChanged(nameof(IsActiveTaskVisible));
    }

    [ObservableProperty]
    private string _startTaskTitle = string.Empty;

    [ObservableProperty]
    private string _startTaskContext = string.Empty;

    public bool ShowStartForm => ActiveTask == null && !_extensionHasActiveTask;
    public bool IsActiveTaskVisible => ActiveTask != null || _extensionHasActiveTask;

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
        IsMonitoring && _focusScoreService.HasRealScore && IsAiConfigured;

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
        string.IsNullOrEmpty(AiModelDisplay)
            ? AiProviderDisplay
            : $"{AiProviderDisplay} · {AiModelDisplay}";

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

    public bool ShowCheckingMessage => IsMonitoring && !HasCurrentFocusResult;

    public bool ShowMarkOverrideButton => 
        HasCurrentFocusResult && 
        !IsClassifying && 
        !IsFocusBotWindow;

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

    private int _liveDistractionCount;
    public int LiveDistractionCount
    {
        get => _liveDistractionCount;
        private set => SetProperty(ref _liveDistractionCount, value);
    }

    public int TodayFocusScoreBucket
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public string TodayDateLabel
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public double TodayFocusedPercent
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public double TodayUnclearPercent
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public double TodayDistractedPercent
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public string TodayFocusedPercentText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "0% focused";

    public string TodayFocusedTimeText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "00:00:00";

    public string TodayDistractedTimeText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "00:00:00";

    public int TodayDistractionCount
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public string TodayAverageDistractionCostText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "—";

    public string TodayMostPopularDistractionAppText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "—";

    public string TodayLongestFocusedSessionText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "—";

    public bool HasTodayAnalytics
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public bool ShowTodayFocusScoreChip =>
        HasTodayAnalytics && IsAiConfigured && TodayFocusScoreBucket > 0;

    private bool _isAnalyticsExpanded = true;
    public bool IsAnalyticsExpanded
    {
        get => _isAnalyticsExpanded;
        set => SetProperty(ref _isAnalyticsExpanded, value);
    }

    private bool _isExtensionConnected;
    public bool IsExtensionConnected
    {
        get => _isExtensionConnected;
        private set
        {
            if (SetProperty(ref _isExtensionConnected, value))
                OnPropertyChanged(nameof(ShowExtensionPromo));
        }
    }

    /// <summary>
    /// True when the foreground window is Microsoft Edge or Google Chrome (used to show extension promo only for supported browsers).
    /// </summary>
    public bool IsForegroundBrowserEdgeOrChrome =>
        !string.IsNullOrEmpty(CurrentProcessName) && EdgeOrChromeProcessNames.Contains(CurrentProcessName);

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

    private TaskStartedPayload? _remoteTaskFromExtension;
    private FocusStatusPayload? _remoteTaskFocusStatus;
    private DateTime? _remoteTaskStartedAtUtc;

    /// <summary>
    /// When the extension has an active task, we show it in the board. Null when no remote task.
    /// </summary>
    public TaskStartedPayload? RemoteTaskFromExtension
    {
        get => _remoteTaskFromExtension;
        private set => SetProperty(ref _remoteTaskFromExtension, value);
    }

    /// <summary>
    /// Latest focus status for the remote task (when extension is leading).
    /// </summary>
    public FocusStatusPayload? RemoteTaskFocusStatus
    {
        get => _remoteTaskFocusStatus;
        private set => SetProperty(ref _remoteTaskFocusStatus, value);
    }

    /// <summary>
    /// In-progress tasks to display: local InProgressTasks, or one synthetic task when extension has the task.
    /// </summary>
    public ObservableCollection<UserTask> DisplayInProgressTasks { get; } = new();

    public FocusPageViewModel(
        ITaskRepository repo,
        IWindowMonitorService windowMonitor,
        ITimeTrackingService timeTracking,
        IIdleDetectionService idleDetection,
        INavigationService navigationService,
        ILlmService llmService,
        ISettingsService settingsService,
        IFocusScoreService focusScoreService,
        ITrialService trialService,
        IDistractionDetectorService distractionDetectorService,
        IDistractionEventRepository distractionEventRepository,
        IDailyAnalyticsService dailyAnalyticsService,
        IAlignmentCacheRepository alignmentCacheRepository,
        IIntegrationService? integrationService = null,
        IUIThreadDispatcher? uiDispatcher = null
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
        _distractionDetectorService = distractionDetectorService;
        _distractionEventRepository = distractionEventRepository;
        _dailyAnalyticsService = dailyAnalyticsService;
        _alignmentCacheRepository = alignmentCacheRepository;
        _integrationService = integrationService;
        _uiDispatcher = uiDispatcher;
        _distractionDetectorService.DistractionEventCreated += OnDistractionEventCreated;
        _windowMonitor.ForegroundWindowChanged += OnForegroundWindowChanged;
        _timeTracking.Tick += OnTimeTrackingTick;
        _idleDetection.UserBecameIdle += OnUserBecameIdle;
        _idleDetection.UserBecameActive += OnUserBecameActive;

        if (_integrationService != null)
        {
            _integrationService.ExtensionConnectionChanged += OnExtensionConnectionChanged;
            _integrationService.TaskStartedReceived += OnIntegrationTaskStarted;
            _integrationService.TaskEndedReceived += OnIntegrationTaskEnded;
            _integrationService.FocusStatusReceived += OnIntegrationFocusStatusReceived;
        }

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

    private static string FormatTimeSpan(TimeSpan timeSpan) =>
        FormatElapsed((long)timeSpan.TotalSeconds);

    private void OnTimeTrackingTick(object? sender, EventArgs e)
    {
        if (InProgressTasks.Count == 0)
        {
            if (RemoteTaskFromExtension != null && _remoteTaskStartedAtUtc.HasValue)
            {
                var elapsed = (long)(DateTime.UtcNow - _remoteTaskStartedAtUtc.Value).TotalSeconds;
                TaskElapsedTime = FormatElapsed(elapsed);
            }
            return;
        }
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
        var status = FocusScore switch
        {
            >= 6 => FocusStatus.Focused,
            >= 4 => FocusStatus.Neutral,
            _ => FocusStatus.Distracted,
        };
        _ = _distractionDetectorService.OnSampleAsync(
            taskId,
            status,
            CurrentProcessName,
            CurrentWindowTitle,
            DateTime.UtcNow
        );
        _ = _dailyAnalyticsService.UpdateForTickAsync(DateTime.UtcNow, status);
        OnPropertyChanged(nameof(IsFocusScorePercentVisible));
        RaiseFocusOverlayStateChanged();
        _ = RefreshTodaySummaryAsync();
        _secondsSinceLastPersist++;
        if (_secondsSinceLastPersist >= PersistIntervalSeconds)
        {
            _secondsSinceLastPersist = 0;
            _ = PersistElapsedTimeAsync(taskId);
            _ = _focusScoreService.PersistSegmentsAsync();
        }
    }

    private void OnDistractionEventCreated(object? sender, DistractionEvent e)
    {
        if (!HasActiveTask())
            return;

        var currentTaskId = InProgressTasks[0].TaskId;
        if (!string.Equals(e.TaskId, currentTaskId, StringComparison.Ordinal))
            return;

        LiveDistractionCount++;
        _ = _dailyAnalyticsService.RegisterDistractionEventAsync(e);
    }

    public event EventHandler<SessionDistractionSummary>? SessionSummaryReady;

    private async Task ShowSessionDistractionSummaryAsync(string taskId)
    {
        if (_sessionStartUtc is null)
            return;

        if (InProgressTasks.Count == 0)
            return;

        var fromUtc = _sessionStartUtc.Value;
        var toUtc = DateTime.UtcNow;
        var events =
            await _distractionEventRepository
                .GetEventsForTaskBetweenAsync(taskId, fromUtc, toUtc)
                .ConfigureAwait(false) ?? Array.Empty<DistractionEvent>();

        var totalCount = events.Count;
        var topApps = events
            .GroupBy(e => e.ProcessName)
            .Select(g => new AppDistractionSummary
            {
                AppName = g.Key,
                DistractionCount = g.Count(),
                DistractedDurationSeconds = g.Sum(x => x.DistractedDurationSecondsAtEmit),
            })
            .OrderByDescending(a => a.DistractedDurationSeconds)
            .ThenByDescending(a => a.DistractionCount)
            .ThenBy(a => a.AppName)
            .Take(3)
            .ToList();

        var summary = new SessionDistractionSummary
        {
            TotalDistractionCount = totalCount,
            TopApps = topApps,
        };

        SessionSummaryReady?.Invoke(this, summary);
    }

    private void OnUserBecameIdle(object? sender, EventArgs e)
    {
        if (InProgressTasks.Count == 0)
            return;

        var backdateSeconds = (int)_idleDetection.IdleThreshold.TotalSeconds;
        _taskElapsedSeconds = Math.Max(0L, _taskElapsedSeconds - backdateSeconds);
        _windowElapsedSeconds = Math.Max(0L, _windowElapsedSeconds - backdateSeconds);
        var key = GetCurrentWindowKey();
        if (!string.IsNullOrEmpty(key))
        {
            var current = _perWindowTotalSeconds.GetValueOrDefault(key, 0L);
            _perWindowTotalSeconds[key] = Math.Max(0L, current - backdateSeconds);
        }

        TaskElapsedTime = FormatElapsed(_taskElapsedSeconds);
        WindowElapsedTime = FormatElapsed(_windowElapsedSeconds);
        WindowTotalElapsedTime = FormatElapsed(_perWindowTotalSeconds.GetValueOrDefault(key ?? string.Empty, 0L));

        _focusScoreService.PauseCurrentSegment(_idleDetection.IdleThreshold);
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
        OnPropertyChanged(nameof(IsForegroundBrowserEdgeOrChrome));
        OnPropertyChanged(nameof(ShowExtensionPromo));
        _windowElapsedSeconds = 0;
        WindowElapsedTime = FormatElapsed(0);
        var newKey = GetCurrentWindowKey(e.ProcessName, e.WindowTitle);
        var newTotal = _perWindowTotalSeconds.GetValueOrDefault(newKey, 0);
        WindowTotalElapsedTime = FormatElapsed(newTotal);

        (string taskId, string description, string? context)? effectiveTask = InProgressTasks.Count > 0
            ? (InProgressTasks[0].TaskId, InProgressTasks[0].Description, InProgressTasks[0].Context)
            : RemoteTaskFromExtension != null
                ? (RemoteTaskFromExtension.TaskId, RemoteTaskFromExtension.TaskText, RemoteTaskFromExtension.TaskHints)
                : null;

        if (effectiveTask == null)
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

        var (taskId, taskDescription, taskContext) = effectiveTask.Value;
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
            taskId,
            contextHash,
            e.WindowTitle,
            e.ProcessName
        );

        if (_integrationService is { IsExtensionConnected: true })
            _ = _integrationService.SendDesktopForegroundAsync(e.ProcessName, e.WindowTitle);

        if (IsBrowserProcess(e.ProcessName) && _integrationService is { IsExtensionConnected: true })
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
                MarkOverrideButtonText = FocusScore >= 6 ? "Mark as distracting" : "Mark as focused";
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
            LiveDistractionCount = 0;
            StartMonitoring();
        }
        else
            StopMonitoringAndResetFocusState();
        IsMonitoring = InProgressTasks.Count > 0;
        OnPropertyChanged(nameof(FocusStatusIcon));
        OnPropertyChanged(nameof(ShowCheckingMessage));
        RefreshDisplayInProgressTasks();
        await RefreshAiSettingsAsync();
        await RefreshTodaySummaryAsync();
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
        IsAiConfigured = await _llmService.IsConfiguredAsync();
        if (IsAiConfigured)
            AiRequestError = string.Empty;
        OnPropertyChanged(nameof(IsFocusScoreVisible));
        OnPropertyChanged(nameof(IsFocusScorePercentVisible));
        OnPropertyChanged(nameof(ShowTodayFocusScoreChip));
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

    private async Task RefreshTodaySummaryAsync()
    {
        var summary = await _dailyAnalyticsService
            .GetTodaySummaryAsync(DateTime.Now)
            .ConfigureAwait(false);

        if (summary is null)
        {
            HasTodayAnalytics = false;
            TodayFocusScoreBucket = 0;
            TodayFocusedTimeText = "00:00:00";
            TodayDistractedTimeText = "00:00:00";
            TodayDistractionCount = 0;
            TodayAverageDistractionCostText = "—";
            TodayMostPopularDistractionAppText = "—";
            TodayLongestFocusedSessionText = "—";
            TodayDateLabel = string.Empty;
            TodayFocusedPercent = 0;
            TodayUnclearPercent = 0;
            TodayDistractedPercent = 0;
            TodayFocusedPercentText = "0% focused";
            OnPropertyChanged(nameof(ShowTodayFocusScoreChip));
            return;
        }

        HasTodayAnalytics = true;
        TodayFocusScoreBucket = summary.FocusScoreBucket;
        TodayFocusedTimeText = FormatTimeSpan(summary.FocusedTime);
        TodayDistractedTimeText = FormatTimeSpan(summary.DistractedTime);
        TodayDistractionCount = summary.DistractionCount;
        TodayAverageDistractionCostText = summary.AverageDistractionDuration.HasValue
            ? FormatTimeSpan(summary.AverageDistractionDuration.Value)
            : "—";
        TodayMostPopularDistractionAppText = !string.IsNullOrEmpty(
            summary.MostPopularDistractionApp
        )
            ? summary.MostPopularDistractionApp
            : "—";
        TodayLongestFocusedSessionText = summary.LongestFocusedSession.HasValue
            ? FormatTimeSpan(summary.LongestFocusedSession.Value)
            : "—";
        TodayDateLabel = summary
            .AnalyticsDateLocal.ToDateTime(TimeOnly.MinValue)
            .ToString("ddd, MMM d, yyyy");

        var totalSeconds = summary.FocusedTime.TotalSeconds + summary.DistractedTime.TotalSeconds;
        if (totalSeconds <= 0)
        {
            TodayFocusedPercent = 0;
            TodayUnclearPercent = 0;
            TodayDistractedPercent = 0;
            TodayFocusedPercentText = "0% focused";
        }
        else
        {
            var focusedShare = summary.FocusedTime.TotalSeconds / totalSeconds;
            TodayFocusedPercent = focusedShare;
            TodayUnclearPercent = 0;
            TodayDistractedPercent = summary.DistractedTime.TotalSeconds / totalSeconds;
            var focusedPercentRounded = (int)
                Math.Round(focusedShare * 100, MidpointRounding.AwayFromZero);
            TodayFocusedPercentText = $"{focusedPercentRounded}% focused";
        }

        OnPropertyChanged(nameof(ShowTodayFocusScoreChip));
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
    private void ToggleAnalytics() => IsAnalyticsExpanded = !IsAnalyticsExpanded;

    [RelayCommand(CanExecute = nameof(CanStartTask))]
    private async Task StartTaskAsync()
    {
        if (string.IsNullOrWhiteSpace(StartTaskTitle))
            return;

        if (_extensionHasActiveTask && _integrationService?.IsExtensionConnected == true)
        {
            IntegrationBlockedReason = "A task is already in progress in the browser extension. End it there first.";
            return;
        }
        IntegrationBlockedReason = null;

        var context = string.IsNullOrWhiteSpace(StartTaskContext) ? null : StartTaskContext.Trim();
        
        // Create task directly as InProgress
        var task = await _repo.AddTaskAsync(StartTaskTitle.Trim(), context);
        await _repo.SetStatusToAsync(task.TaskId, TaskStatus.InProgress);
        
        // Clear form
        StartTaskTitle = string.Empty;
        StartTaskContext = string.Empty;
        
        // Reload board which will start monitoring
        await LoadBoardAsync();
        
        // Set ActiveTask
        if (InProgressTasks.Count > 0)
        {
            ActiveTask = InProgressTasks[0];
            await NotifyTaskStartedAsync(ActiveTask);
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
        if (ActiveTask == null && InProgressTasks.Count == 0)
            return;

        var taskToEnd = ActiveTask ?? InProgressTasks.FirstOrDefault();
        if (taskToEnd == null)
            return;

        await FinalizeFocusScoreAndPersistAsync(taskToEnd.TaskId);
        await _repo.SetStatusToAsync(taskToEnd.TaskId, TaskStatus.Done);
        await _dailyAnalyticsService.ReloadTodayFromDbAsync();
        
        InProgressTasks.Clear();
        ActiveTask = null;
        
        StopMonitoringAndResetFocusState();
        IsMonitoring = false;
        OnPropertyChanged(nameof(FocusStatusIcon));
        OnPropertyChanged(nameof(ShowCheckingMessage));
        RefreshDisplayInProgressTasks();
        
        await RefreshTodaySummaryAsync();
        
        if (_integrationService?.IsExtensionConnected == true)
        {
            await _integrationService.SendTaskEndedAsync(taskToEnd.TaskId);
        }
    }

    [RelayCommand]
    private void PauseTask()
    {
        if (InProgressTasks.Count == 0)
            return;

        _isTaskPaused = true;
        OnPropertyChanged(nameof(IsTaskPaused));

        _focusScoreService.PauseCurrentSegment();
        _timeTracking.Stop();
        _windowMonitor.Stop();

        RaiseFocusOverlayStateChanged();
    }

    [RelayCommand]
    private void ResumeTask()
    {
        if (InProgressTasks.Count == 0)
            return;

        _isTaskPaused = false;
        OnPropertyChanged(nameof(IsTaskPaused));

        _timeTracking.Start();
        _windowMonitor.Start();

        RaiseFocusOverlayStateChanged();
    }

    [RelayCommand]
    private void ViewHistory()
    {
        // TODO: Navigate to History page when it's created
        // _navigationService.NavigateTo<HistoryPage>();
    }

    [RelayCommand]
    private void OpenSettings() => _navigationService.NavigateToSettings();

    [RelayCommand]
    private void OpenHowItWorks() => ShowHowItWorksRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task MarkFocusOverrideAsync()
    {
        if (InProgressTasks.Count == 0)
            return;

        int newScore = FocusScore >= 6 ? 2 : 9;
        string newReason = FocusScore >= 6 ? "Manually marked as Distracting" : "Manually marked as Focused";

        var taskId = InProgressTasks[0].TaskId;
        var taskDescription = InProgressTasks[0].Description;
        var taskContext = InProgressTasks[0].Context;
        var contextHash = HashHelper.ComputeWindowContextHash(CurrentProcessName, CurrentWindowTitle);
        var taskContentHash = HashHelper.ComputeTaskContentHash(taskDescription, taskContext);

        var entry = new AlignmentCacheEntry
        {
            ContextHash = contextHash,
            TaskContentHash = taskContentHash,
            Score = newScore,
            Reason = newReason,
            CreatedAt = DateTime.UtcNow
        };

        var windowContext = new WindowContext
        {
            ContextHash = contextHash,
            ProcessName = CurrentProcessName,
            WindowTitle = HashHelper.NormalizeWindowTitle(CurrentWindowTitle)
        };

        await _alignmentCacheRepository.SaveAsync(windowContext, entry);
        
        await _focusScoreService.UpdateHistoricalSegmentsAsync(taskId, contextHash, newScore);
        await _dailyAnalyticsService.ReloadTodayFromDbAsync();

        FocusScore = newScore;
        FocusReason = newReason;
        MarkOverrideButtonText = newScore >= 6 ? "Mark as distracting" : "Mark as focused";
        _focusScoreService.UpdatePendingSegmentScore(newScore);

        OnPropertyChanged(nameof(FocusScoreCategory));
        OnPropertyChanged(nameof(FocusStatusIcon));
        RaiseFocusOverlayStateChanged();
        await RefreshTodaySummaryAsync();
    }

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

    /// <summary>
    /// When set, the extension has an active task and the user cannot start a local one. Shown in the UI.
    /// </summary>
    private string? _integrationBlockedReason;
    public string? IntegrationBlockedReason
    {
        get => _integrationBlockedReason;
        set => SetProperty(ref _integrationBlockedReason, value);
    }

    private async Task FinalizeFocusScoreAndPersistAsync(string taskId)
    {
        _focusScoreService.PauseCurrentSegment();
        await _focusScoreService.PersistSegmentsAsync();
        var scorePercent = _focusScoreService.CalculateFocusScorePercent(taskId);
        await _repo.UpdateFocusScoreAsync(taskId, scorePercent);
    }

    private bool HasValidFocusData() => FocusScore > 0 || !string.IsNullOrEmpty(FocusReason);

    private bool HasActiveTask() => InProgressTasks.Count > 0;

    private void StartMonitoring()
    {
        _windowMonitor.Start();
        _timeTracking.Start();
        _idleDetection.Start();
        if (_sessionStartUtc is null)
        {
            _sessionStartUtc = DateTime.UtcNow;
        }
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
        LiveDistractionCount = 0;
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
        var status = FocusScore switch
        {
            >= 6 => FocusStatus.Focused,
            >= 4 => FocusStatus.Neutral,
            _ => FocusStatus.Distracted,
        };
        FocusOverlayStateChanged?.Invoke(
            this,
            new FocusOverlayStateChangedEventArgs
            {
                HasActiveTask = hasActive,
                FocusScorePercent = hasActive ? CurrentFocusScorePercent : 0,
                Status = status,
                IsTaskPaused = _isTaskPaused,
            }
        );

        if (_integrationService is { IsExtensionConnected: true } && hasActive && !_extensionHasActiveTask)
        {
            var classification = FocusScore >= 6 ? "Focused" : FocusScore >= 4 ? "Unclear" : "Distracted";
            _ = _integrationService.SendFocusStatusAsync(new FocusStatusPayload
            {
                TaskId = InProgressTasks[0].TaskId,
                Classification = classification,
                Reason = FocusReason,
                Score = FocusScore,
                FocusScorePercent = CurrentFocusScorePercent,
                ContextType = IsBrowserProcess(CurrentProcessName) ? "browser" : "desktop",
                ContextTitle = CurrentWindowTitle
            });
        }
    }

    private static bool IsBrowserProcess(string processName) =>
        BrowserProcessNames.Contains(processName);

    private void OnExtensionConnectionChanged(object? sender, bool connected)
    {
        void updateAndMaybeSendState()
        {
            IsExtensionConnected = connected;

            if (!connected)
            {
                _extensionHasActiveTask = false;
                IntegrationBlockedReason = null;
                return;
            }

            if (_integrationService == null)
                return;

            if (!HasActiveTask())
            {
                _ = _integrationService.SendHandshakeAsync(false, null, null, null);
                return;
            }

            var task = InProgressTasks.Count > 0 ? InProgressTasks[0] : null;
            if (task != null)
            {
                _ = _integrationService.SendTaskStartedAsync(task.TaskId, task.Description, task.Context);
            }
        }

        if (_uiDispatcher != null)
        {
            _ = _uiDispatcher.RunOnUIThreadAsync(() =>
            {
                updateAndMaybeSendState();
                return Task.CompletedTask;
            });
        }
        else
        {
            updateAndMaybeSendState();
        }
    }

    private void OnIntegrationTaskStarted(object? sender, TaskStartedPayload payload)
    {
        _extensionHasActiveTask = true;
        _remoteTaskStartedAtUtc = !string.IsNullOrEmpty(payload.StartedAt) && DateTime.TryParse(payload.StartedAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed.ToUniversalTime()
            : DateTime.UtcNow;

        void apply()
        {
            _windowMonitor.Start();
            _timeTracking.Start();
            RemoteTaskFromExtension = payload;
            RemoteTaskFocusStatus = null;
            var initialElapsed = (long)(DateTime.UtcNow - _remoteTaskStartedAtUtc!.Value).TotalSeconds;
            TaskElapsedTime = FormatElapsed(initialElapsed);
            RefreshDisplayInProgressTasks();
        }

        if (_uiDispatcher != null)
        {
            _ = _uiDispatcher.RunOnUIThreadAsync(() =>
            {
                apply();
                return Task.CompletedTask;
            });
        }
        else
        {
            apply();
        }
    }

    private void OnIntegrationTaskEnded(object? sender, EventArgs e)
    {
        _extensionHasActiveTask = false;
        if (_uiDispatcher != null)
        {
            _ = _uiDispatcher.RunOnUIThreadAsync(() =>
            {
                ClearRemoteTask();
                if (InProgressTasks.Count == 0)
                {
                    _windowMonitor.Stop();
                    _timeTracking.Stop();
                }
                return Task.CompletedTask;
            });
        }
        else
        {
            ClearRemoteTask();
            if (InProgressTasks.Count == 0)
            {
                _windowMonitor.Stop();
                _timeTracking.Stop();
            }
        }
    }

    private void OnIntegrationFocusStatusReceived(object? sender, FocusStatusPayload payload)
    {
        if (RemoteTaskFromExtension == null || payload.TaskId != RemoteTaskFromExtension.TaskId)
            return;
        void apply()
        {
            RemoteTaskFocusStatus = payload;
            FocusScore = payload.Score;
            FocusReason = payload.Reason;
            CurrentProcessName = payload.ContextType;
            CurrentWindowTitle = payload.ContextTitle;
            OnPropertyChanged(nameof(IsForegroundBrowserEdgeOrChrome));
            OnPropertyChanged(nameof(ShowExtensionPromo));
            CurrentFocusScorePercent = payload.FocusScorePercent;
            HasCurrentFocusResult = true;
            IsClassifying = false;
            OnPropertyChanged(nameof(IsFocusScoreVisible));
            OnPropertyChanged(nameof(FocusScoreCategory));
            OnPropertyChanged(nameof(FocusStatusIcon));
            OnPropertyChanged(nameof(FocusAccentBrushKey));
            OnPropertyChanged(nameof(IsFocusScorePercentVisible));
            RaiseFocusOverlayStateChanged();
        }
        if (_uiDispatcher != null)
            _ = _uiDispatcher.RunOnUIThreadAsync(() => { apply(); return Task.CompletedTask; });
        else
            apply();
    }

    private void ClearRemoteTask()
    {
        _extensionHasActiveTask = false;
        _remoteTaskStartedAtUtc = null;
        IntegrationBlockedReason = null;
        RemoteTaskFromExtension = null;
        RemoteTaskFocusStatus = null;
        RefreshDisplayInProgressTasks();
    }

    private void RefreshDisplayInProgressTasks()
    {
        DisplayInProgressTasks.Clear();
        if (InProgressTasks.Count > 0)
        {
            foreach (var t in InProgressTasks)
                DisplayInProgressTasks.Add(t);
        }
        else if (RemoteTaskFromExtension != null)
        {
            DisplayInProgressTasks.Add(new UserTask
            {
                TaskId = RemoteTaskFromExtension.TaskId,
                Description = RemoteTaskFromExtension.TaskText,
                Context = RemoteTaskFromExtension.TaskHints,
                Status = TaskStatus.InProgress
            });
        }
        IsMonitoring = DisplayInProgressTasks.Count > 0;
        OnPropertyChanged(nameof(FocusStatusIcon));
        OnPropertyChanged(nameof(ShowCheckingMessage));
    }

    /// <summary>
    /// Notifies the extension that a task has started (Full Mode). Called after a task moves to InProgress.
    /// </summary>
    public async Task NotifyTaskStartedAsync(UserTask task)
    {
        _extensionHasActiveTask = false;
        if (_integrationService is { IsExtensionConnected: true })
        {
            await _integrationService.SendTaskStartedAsync(task.TaskId, task.Description, task.Context);
        }
    }

    /// <summary>
    /// Notifies the extension that the current task has ended.
    /// </summary>
    public async Task NotifyTaskEndedAsync(string taskId)
    {
        if (_integrationService is { IsExtensionConnected: true })
        {
            await _integrationService.SendTaskEndedAsync(taskId);
        }
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
            await ClassifyAndUpdateFocusAsync(taskDescription, taskContext, processName, windowTitle);
            return;
        }

        var browserContext = _integrationService.LastBrowserContext;
        if (browserContext != null && !string.IsNullOrEmpty(browserContext.Url))
        {
            var domain = Uri.TryCreate(browserContext.Url, UriKind.Absolute, out var uri) && uri.IsAbsoluteUri
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
            await ClassifyAndUpdateFocusAsync(taskDescription, taskContext, processName, combinedTitle);
        }
        else
        {
            await ClassifyAndUpdateFocusAsync(taskDescription, taskContext, processName, windowTitle);
        }
    }
}

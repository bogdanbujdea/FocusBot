using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusBot.Core;
using FocusBot.Core.Configuration;
using FocusBot.Core.Entities;
using FocusBot.Core.Events;
using FocusBot.Core.Helpers;
using FocusBot.Core.Interfaces;

namespace FocusBot.App.ViewModels;

public partial class FocusPageViewModel : ObservableObject
{
    private readonly ISessionRepository _repo;
    private readonly INavigationService _navigationService;
    private readonly ISettingsService _settingsService;
    private readonly IFocusSessionOrchestrator _sessionOrchestrator;
    public AccountSettingsViewModel AccountSection { get; }
    private readonly IIntegrationService? _integrationService;
    private readonly IUIThreadDispatcher? _uiDispatcher;

    /// <summary>
    /// Raised when the user requests to open the How it works guide (e.g. Help button). The view shows the dialog.
    /// </summary>
    public event EventHandler? ShowHowItWorksRequested;

    /// <summary>
    /// Raised when focus overlay state changes (score, status, or active session).
    /// </summary>
    public event EventHandler<FocusOverlayStateChangedEventArgs>? FocusOverlayStateChanged;

    private long _sessionElapsedSeconds;

    // Single local in-progress session
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowStartForm))]
    [NotifyPropertyChangedFor(nameof(IsActiveSessionVisible))]
    private UserSession? _activeSession;

    [ObservableProperty]
    private string _startSessionTitle = string.Empty;

    [ObservableProperty]
    private string _startSessionContext = string.Empty;

    public bool ShowStartForm => ActiveSession == null;
    public bool IsActiveSessionVisible => ActiveSession != null;

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
    /// Gets whether the current session is paused (time tracking and monitoring stopped).
    /// </summary>
    public bool IsSessionPaused => _sessionOrchestrator.IsSessionPaused;

    public bool IsFocusScoreVisible => IsMonitoring && IsAiConfigured;

    public bool IsFocusResultVisible => ActiveSession != null;

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

    public bool IsFocusScorePercentVisible => IsMonitoring && IsAiConfigured;

    public string SessionElapsedTime
    {
        get;
        set => SetProperty(ref field, value);
    } = "00:00:00";

    private string? _aiRequestError;
    public string? AiRequestError
    {
        get => _aiRequestError;
        set { if (SetProperty(ref _aiRequestError, value)) { } }
    }

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

    public bool ShowMarkOverrideButton => HasCurrentFocusResult && !IsClassifying && !IsNeutralApp;

    /// <summary>
    /// True when the current foreground app is considered neutral (not subject to focus classification).
    /// </summary>
    private bool IsNeutralApp =>
        FocusReason.Contains("neutral", StringComparison.OrdinalIgnoreCase);

    public string MarkOverrideButtonText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "Mark as distracting";

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
        ISessionRepository repo,
        INavigationService navigationService,
        ISettingsService settingsService,
        IFocusSessionOrchestrator sessionOrchestrator,
        AccountSettingsViewModel accountSection,
        IIntegrationService? integrationService = null,
        IUIThreadDispatcher? uiDispatcher = null
    )
    {
        _repo = repo;
        _navigationService = navigationService;
        _settingsService = settingsService;
        _sessionOrchestrator = sessionOrchestrator;
        AccountSection = accountSection;
        _integrationService = integrationService;
        _uiDispatcher = uiDispatcher;

        _sessionOrchestrator.StateChanged += OnOrchestratorStateChanged;

        if (_integrationService != null)
        {
            _integrationService.ExtensionConnectionChanged += OnExtensionConnectionChanged;
        }

        _ = LoadBoardAsync();
    }

    private void OnOrchestratorStateChanged(object? sender, FocusSessionStateChangedEventArgs e)
    {
        void UpdateState()
        {
            _sessionElapsedSeconds = e.SessionElapsedSeconds;
            SessionElapsedTime = TimeFormatHelper.FormatElapsed(e.SessionElapsedSeconds);
            CurrentFocusScorePercent = e.FocusScorePercent;
            IsClassifying = e.IsClassifying;
            FocusScore = e.FocusScore;
            FocusReason = e.FocusReason;
            HasCurrentFocusResult = e.HasCurrentFocusResult;
            AiRequestError = e.AiRequestError;
            CurrentProcessName = e.CurrentProcessName;
            CurrentWindowTitle = e.CurrentWindowTitle;
            MarkOverrideButtonText = e.FocusScore >= 6 ? "Mark as distracting" : "Mark as focused";

            OnPropertyChanged(nameof(IsSessionPaused));
            OnPropertyChanged(nameof(IsFocusScoreVisible));
            OnPropertyChanged(nameof(IsFocusResultVisible));
            OnPropertyChanged(nameof(FocusScoreCategory));
            OnPropertyChanged(nameof(FocusStatusIcon));
            OnPropertyChanged(nameof(FocusAccentBrushKey));
            OnPropertyChanged(nameof(IsFocusScorePercentVisible));
            OnPropertyChanged(nameof(ShowCheckingMessage));
            OnPropertyChanged(nameof(IsForegroundBrowserEdgeOrChrome));
            OnPropertyChanged(nameof(ShowExtensionPromo));
            OnPropertyChanged(nameof(ShowMarkOverrideButton));

            RaiseFocusOverlayStateChanged();
        }

        if (_uiDispatcher != null)
        {
            _ = _uiDispatcher.RunOnUIThreadAsync(() =>
            {
                UpdateState();
                return Task.CompletedTask;
            });
        }
        else
        {
            UpdateState();
        }
    }

    private async Task LoadBoardAsync()
    {
        ActiveSession = null;

        var inProgress = await _repo.GetInProgressSessionAsync();
        if (inProgress != null)
        {
            ActiveSession = inProgress;
        }

        if (HasActiveSession())
        {
            var session = ActiveSession!;
            _sessionElapsedSeconds = session.TotalElapsedSeconds;
            SessionElapsedTime = TimeFormatHelper.FormatElapsed(_sessionElapsedSeconds);
            CurrentFocusScorePercent = 0;
            OnPropertyChanged(nameof(IsFocusScorePercentVisible));
            _sessionOrchestrator.StartSession(session, session.TotalElapsedSeconds);
        }
        else
        {
            ResetFocusState();
        }
        UpdateMonitoringState();

        // Sync backend session state so EndSession can close it properly
        await _sessionOrchestrator.SyncBackendSessionAsync();
    }

    /// <summary>
    /// Refreshes the displayed AI provider and model from settings. Call when returning to the board so the corner label is up to date.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStartSession))]
    private async Task StartSessionAsync()
    {
        if (string.IsNullOrWhiteSpace(StartSessionTitle))
            return;

        var context = string.IsNullOrWhiteSpace(StartSessionContext)
            ? null
            : StartSessionContext.Trim();

        // Create session directly as InProgress
        var session = await _repo.AddSessionAsync(StartSessionTitle.Trim(), context);
        await _repo.SetActiveAsync(session.SessionId);

        // Clear form
        StartSessionTitle = string.Empty;
        StartSessionContext = string.Empty;

        ActiveSession = session;
        _sessionOrchestrator.StartSession(session);
        UpdateMonitoringState();
    }

    private bool CanStartSession() => !string.IsNullOrWhiteSpace(StartSessionTitle);

    partial void OnStartSessionTitleChanged(string value)
    {
        StartSessionCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task EndSessionAsync()
    {
        if (ActiveSession == null)
            return;

        await _sessionOrchestrator.EndSessionAsync();
        ActiveSession = null;
        ResetFocusState();
        UpdateMonitoringState();
    }

    [RelayCommand]
    private void PauseSession()
    {
        if (ActiveSession == null)
            return;

        _sessionOrchestrator.PauseSession();
        OnPropertyChanged(nameof(IsSessionPaused));
        RaiseFocusOverlayStateChanged();
    }

    [RelayCommand]
    private void ResumeSession()
    {
        if (ActiveSession == null)
            return;

        _sessionOrchestrator.ResumeSession();
        OnPropertyChanged(nameof(IsSessionPaused));
        RaiseFocusOverlayStateChanged();
    }

    [RelayCommand]
    private void OpenSettings() => _navigationService.NavigateToSettings();

    [RelayCommand]
    private void OpenHowItWorks() => ShowHowItWorksRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task MarkFocusOverrideAsync()
    {
        if (ActiveSession == null)
            return;

        int newScore = FocusScore >= 6 ? 2 : 9;
        string newReason =
            FocusScore >= 6 ? "Manually marked as Distracting" : "Manually marked as Focused";

        await _sessionOrchestrator.RecordManualOverrideAsync(newScore, newReason);
    }

    /// <summary>
    /// Returns true if the user has not yet seen the How it works guide (first run).
    /// </summary>
    public async Task<bool> GetHasSeenHowItWorksGuideAsync()
    {
        var value = await _settingsService.GetSettingAsync<bool>(
            SettingsKeys.HasSeenHowItWorksGuide
        );
        return value == true;
    }

    /// <summary>
    /// Marks the How it works guide as seen so it is not shown automatically again.
    /// </summary>
    public Task SetHasSeenHowItWorksGuideAsync() =>
        _settingsService.SetSettingAsync(SettingsKeys.HasSeenHowItWorksGuide, true);

    private bool HasActiveSession() => ActiveSession != null;

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
        var hasActive = HasActiveSession();
        var isLoading = hasActive && !HasCurrentFocusResult;
        var hasError = AiRequestError != null;
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
                HasActiveSession = hasActive,
                FocusScorePercent = hasActive ? CurrentFocusScorePercent : 0,
                Status = status,
                IsSessionPaused = IsSessionPaused,
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
            return "Foqus — No active session";

        if (IsSessionPaused)
            return "Foqus — Paused";

        if (hasError)
            return $"Foqus — Error: {AiRequestError}";

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
        IsMonitoring = ActiveSession != null;
        OnPropertyChanged(nameof(ShowStartForm));
        OnPropertyChanged(nameof(IsActiveSessionVisible));
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
}

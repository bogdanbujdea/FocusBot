using System.ComponentModel;
using System.Threading;
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
    private readonly INavigationService _navigationService;
    private readonly ISettingsService _settingsService;
    private readonly IFocusSessionOrchestrator _sessionOrchestrator;
    private readonly IFocusHubClient _focusHubClient;
    private readonly IPlanService _planService;
    public AccountSettingsViewModel AccountSection { get; }

    /// <summary>
    /// Child ViewModel for the current foreground window status bar.
    /// </summary>
    public FocusStatusViewModel Status { get; }

    private readonly IIntegrationService? _integrationService;
    private readonly IUIThreadDispatcher? _uiDispatcher;

    /// <summary>
    /// Raised when the user requests to open the How it works guide (e.g. Help button). The view shows the dialog.
    /// </summary>
    public event EventHandler? ShowHowItWorksRequested;

    /// <summary>
    /// Raised when the user should see the Cloud BYOK API key prompt (desktop). The view shows the dialog.
    /// </summary>
    public event EventHandler? ShowBYOKKeyPromptRequested;

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

    /// <summary>
    /// Gets whether the current session is paused (time tracking and monitoring stopped).
    /// </summary>
    public bool IsSessionPaused => _sessionOrchestrator.IsSessionPaused;

    public bool IsFocusScoreVisible => Status.IsMonitoring && AccountSection.IsAuthenticated;

    public bool IsFocusResultVisible => ActiveSession != null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DistractedBarStarWeight))]
    [NotifyPropertyChangedFor(nameof(FocusedPercentLabel))]
    [NotifyPropertyChangedFor(nameof(DistractedPercentLabel))]
    private int _currentFocusScorePercent;

    public bool IsFocusScorePercentVisible => Status.IsMonitoring && AccountSection.IsAuthenticated;

    /// <summary>Star weight for the distracted segment of the focus bar (100 minus focus score).</summary>
    public int DistractedBarStarWeight => Math.Max(0, 100 - CurrentFocusScorePercent);

    public string FocusedPercentLabel => $"{CurrentFocusScorePercent}% Focused";

    public string DistractedPercentLabel => $"{DistractedBarStarWeight}% Distracted";

    public string FocusedTime
    {
        get;
        private set => SetProperty(ref field, value);
    } = "00:00:00";

    public string DistractedTime
    {
        get;
        private set => SetProperty(ref field, value);
    } = "00:00:00";

    public int DistractionCount
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public string SessionElapsedTime
    {
        get;
        set => SetProperty(ref field, value);
    } = "00:00:00";

    public string? AiRequestError
    {
        get;
        set { if (SetProperty(ref field, value)) { } }
    }

    [ObservableProperty]
    private string? _apiErrorMessage;

    [ObservableProperty]
    private bool _isApiErrorVisible;

    [ObservableProperty]
    private bool _isTrialBannerVisible;

    [ObservableProperty]
    private string _trialCountdownMessage = string.Empty;

    [ObservableProperty]
    private bool _showPostTrialUpgradeBanner;

    private Timer? _trialCountdownTimer;
    private bool _byokPromptShownThisSession;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartSessionCommand))]
    [NotifyCanExecuteChangedFor(nameof(EndSessionCommand))]
    [NotifyPropertyChangedFor(nameof(IsSessionOperationEnabled))]
    private bool _isSessionBusy;

    /// <summary>False while a session API call (start, end, or loading board) is in progress.</summary>
    public bool IsSessionOperationEnabled => !IsSessionBusy;

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
        BrowserProcessNames.IsExtensionSupported(Status.CurrentProcessName);

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
        INavigationService navigationService,
        ISettingsService settingsService,
        IFocusSessionOrchestrator sessionOrchestrator,
        IFocusHubClient focusHubClient,
        IPlanService planService,
        AccountSettingsViewModel accountSection,
        FocusStatusViewModel status,
        IIntegrationService? integrationService = null,
        IUIThreadDispatcher? uiDispatcher = null
    )
    {
        _navigationService = navigationService;
        _settingsService = settingsService;
        _sessionOrchestrator = sessionOrchestrator;
        _focusHubClient = focusHubClient;
        _planService = planService;
        AccountSection = accountSection;
        Status = status;
        _integrationService = integrationService;
        _uiDispatcher = uiDispatcher;

        _sessionOrchestrator.StateChanged += OnOrchestratorStateChanged;

        _focusHubClient.SessionStarted += OnFocusHubSessionStarted;
        _focusHubClient.SessionEnded += OnFocusHubSessionEnded;
        _focusHubClient.SessionPaused += OnFocusHubSessionPaused;
        _focusHubClient.SessionResumed += OnFocusHubSessionResumed;
        _focusHubClient.PlanChanged += OnFocusHubPlanChanged;
        _focusHubClient.ClassificationChanged += OnFocusHubClassificationChanged;

        _planService.PlanChanged += OnPlanServicePlanChanged;

        AccountSection.PropertyChanged += OnAccountSectionPropertyChanged;

        if (_integrationService != null)
        {
            _integrationService.ExtensionConnectionChanged += OnExtensionConnectionChanged;
        }

        _ = LoadBoardAsync();
    }

    private void OnAccountSectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AccountSettingsViewModel.IsAuthenticated))
            _ = UpdateTrialStateAsync();
    }

    private async void OnPlanServicePlanChanged(object? sender, ClientPlanType plan)
    {
        await UpdateTrialStateAsync();
        await TryRaiseByokPromptAsync();
    }

    private async void OnFocusHubPlanChanged(PlanChangedEvent e)
    {
        await _planService.RefreshAsync();
        await UpdateTrialStateAsync();
        await TryRaiseByokPromptAsync();
    }

    private void OnFocusHubClassificationChanged(ClassificationChangedEvent e)
    {
        void Apply()
        {
            _sessionOrchestrator.ApplyRemoteClassificationFromHub(
                e.Source,
                e.Score,
                e.Reason,
                e.ActivityName);
        }

        if (_uiDispatcher != null)
        {
            _ = _uiDispatcher.RunOnUIThreadAsync(() =>
            {
                Apply();
                return Task.CompletedTask;
            });
        }
        else
        {
            Apply();
        }
    }

    private async Task TryRaiseByokPromptAsync()
    {
        if (!AccountSection.IsAuthenticated)
            return;

        var plan = await _planService.GetCurrentPlanAsync();
        if (plan != ClientPlanType.CloudBYOK)
            return;

        if (_byokPromptShownThisSession)
            return;

        var key = await _settingsService.GetApiKeyAsync();
        if (!string.IsNullOrWhiteSpace(key))
            return;

        _byokPromptShownThisSession = true;
        void Raise()
        {
            ShowBYOKKeyPromptRequested?.Invoke(this, EventArgs.Empty);
        }

        if (_uiDispatcher != null)
            await _uiDispatcher.RunOnUIThreadAsync(() =>
            {
                Raise();
                return Task.CompletedTask;
            });
        else
            Raise();
    }

    private void StartTrialCountdownTimer()
    {
        if (_trialCountdownTimer != null)
            return;

        _trialCountdownTimer = new Timer(
            _ => _ = UpdateTrialStateAsync(),
            null,
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(15));
    }

    private void StopTrialCountdownTimer()
    {
        _trialCountdownTimer?.Dispose();
        _trialCountdownTimer = null;
    }

    private async Task UpdateTrialStateAsync()
    {
        if (!AccountSection.IsAuthenticated)
        {
            void ClearUi()
            {
                IsTrialBannerVisible = false;
                ShowPostTrialUpgradeBanner = false;
                TrialCountdownMessage = string.Empty;
                StopTrialCountdownTimer();
            }

            if (_uiDispatcher != null)
                await _uiDispatcher.RunOnUIThreadAsync(() =>
                {
                    ClearUi();
                    return Task.CompletedTask;
                });
            else
                ClearUi();
            return;
        }

        var plan = await _planService.GetCurrentPlanAsync();
        var status = await _planService.GetStatusAsync();
        var endsAt = await _planService.GetTrialEndsAtAsync();
        var endsUtc = endsAt?.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(endsAt.Value, DateTimeKind.Utc)
            : endsAt?.ToUniversalTime();

        var isFoqusTrialWindow =
            plan == ClientPlanType.FreeBYOK
            && status == ClientSubscriptionStatus.Trial
            && endsUtc.HasValue;

        var trialStillActive =
            isFoqusTrialWindow && endsUtc!.Value > DateTime.UtcNow;

        void ApplyUi()
        {
            IsTrialBannerVisible = trialStillActive;
            TrialCountdownMessage = FormatTrialCountdown(endsUtc, trialStillActive);

            ShowPostTrialUpgradeBanner =
                plan == ClientPlanType.FreeBYOK
                && (status == ClientSubscriptionStatus.Expired
                    || (status == ClientSubscriptionStatus.Trial
                        && endsUtc.HasValue
                        && endsUtc.Value <= DateTime.UtcNow));

            if (trialStillActive)
                StartTrialCountdownTimer();
            else
                StopTrialCountdownTimer();
        }

        if (_uiDispatcher != null)
            await _uiDispatcher.RunOnUIThreadAsync(() =>
            {
                ApplyUi();
                return Task.CompletedTask;
            });
        else
            ApplyUi();
    }

    private static string FormatTrialCountdown(DateTime? trialEndsAtUtc, bool trialStillActive)
    {
        if (!trialEndsAtUtc.HasValue)
            return string.Empty;

        var local = trialEndsAtUtc.Value.ToLocalTime();
        if (!trialStillActive)
            return string.Empty;

        var remaining = trialEndsAtUtc.Value - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
            return string.Empty;

        return $"Trial ends {local:MMM d, h:mm tt} ({FormatRemaining(remaining)} left)";
    }

    private static string FormatRemaining(TimeSpan t)
    {
        if (t.TotalHours >= 1)
            return $"{(int)t.TotalHours}h {t.Minutes}m";
        return $"{t.Minutes}m {t.Seconds}s";
    }

    private void OnOrchestratorStateChanged(object? sender, FocusSessionStateChangedEventArgs e)
    {
        void UpdateState()
        {
            _sessionElapsedSeconds = e.SessionElapsedSeconds;
            SessionElapsedTime = TimeFormatHelper.FormatElapsed(e.SessionElapsedSeconds);
            CurrentFocusScorePercent = e.FocusScorePercent;
            FocusedTime = TimeFormatHelper.FormatElapsed(e.FocusedSeconds);
            DistractedTime = TimeFormatHelper.FormatElapsed(e.DistractedSeconds);
            DistractionCount = e.DistractionCount;
            AiRequestError = e.AiRequestError;

            OnPropertyChanged(nameof(IsSessionPaused));
            OnPropertyChanged(nameof(IsFocusScoreVisible));
            OnPropertyChanged(nameof(IsFocusResultVisible));
            OnPropertyChanged(nameof(IsFocusScorePercentVisible));
            OnPropertyChanged(nameof(IsForegroundBrowserEdgeOrChrome));
            OnPropertyChanged(nameof(ShowExtensionPromo));

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
        IsSessionBusy = true;
        try
        {
            ActiveSession = null;

            var session = await _sessionOrchestrator.LoadActiveSessionAsync();
            if (session != null)
            {
                ActiveSession = session;
                _sessionElapsedSeconds = session.TotalElapsedSeconds;
                SessionElapsedTime = TimeFormatHelper.FormatElapsed(_sessionElapsedSeconds);
                CurrentFocusScorePercent = 0;
                FocusedTime = "00:00:00";
                DistractedTime = "00:00:00";
                DistractionCount = 0;
                OnPropertyChanged(nameof(IsFocusScorePercentVisible));
                _sessionOrchestrator.BeginLocalSessionTracking(session, session.TotalElapsedSeconds);
            }
            else
            {
                _sessionOrchestrator.StopLocalTrackingIfActive();
                ResetFocusState();
            }

            UpdateMonitoringState();
            await UpdateTrialStateAsync();
        }
        finally
        {
            IsSessionBusy = false;
        }
    }

    private void OnFocusHubSessionStarted(SessionStartedEvent e)
    {
        void Handle()
        {
            if (ActiveSession != null && ActiveSession.SessionId == e.SessionId.ToString())
                return;

            _ = LoadBoardAsync();
        }

        if (_uiDispatcher != null)
        {
            _ = _uiDispatcher.RunOnUIThreadAsync(() =>
            {
                Handle();
                return Task.CompletedTask;
            });
        }
        else
        {
            Handle();
        }
    }

    private void OnFocusHubSessionEnded(SessionEndedEvent e)
    {
        void Handle()
        {
            _ = LoadBoardAsync();
        }

        if (_uiDispatcher != null)
        {
            _ = _uiDispatcher.RunOnUIThreadAsync(() =>
            {
                Handle();
                return Task.CompletedTask;
            });
        }
        else
        {
            Handle();
        }
    }

    private void OnFocusHubSessionPaused(SessionPausedEvent e)
    {
        void Handle()
        {
            if (ActiveSession == null || ActiveSession.SessionId != e.SessionId.ToString())
                return;

            _sessionOrchestrator.ApplyRemotePause();
            OnPropertyChanged(nameof(IsSessionPaused));
            RaiseFocusOverlayStateChanged();
        }

        if (_uiDispatcher != null)
        {
            _ = _uiDispatcher.RunOnUIThreadAsync(() =>
            {
                Handle();
                return Task.CompletedTask;
            });
        }
        else
        {
            Handle();
        }
    }

    private void OnFocusHubSessionResumed(SessionResumedEvent e)
    {
        void Handle()
        {
            if (ActiveSession == null || ActiveSession.SessionId != e.SessionId.ToString())
                return;

            _sessionOrchestrator.ApplyRemoteResume();
            OnPropertyChanged(nameof(IsSessionPaused));
            RaiseFocusOverlayStateChanged();
        }

        if (_uiDispatcher != null)
        {
            _ = _uiDispatcher.RunOnUIThreadAsync(() =>
            {
                Handle();
                return Task.CompletedTask;
            });
        }
        else
        {
            Handle();
        }
    }

    /// <summary>
    /// Reloads the focus board from the API (active session). Call when authentication becomes
    /// available after the ViewModel was created (e.g. magic-link sign-in after launch).
    /// </summary>
    public Task ReloadBoardAsync() => LoadBoardAsync();

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

        IsSessionBusy = true;
        try
        {
            var result = await _sessionOrchestrator.StartSessionAsync(StartSessionTitle.Trim(), context);
            if (!result.IsSuccess || result.Value is null)
            {
                ApiErrorMessage = result.ErrorMessage ?? "Something went wrong, please try again.";
                IsApiErrorVisible = true;
                return;
            }

            StartSessionTitle = string.Empty;
            StartSessionContext = string.Empty;

            ActiveSession = result.Value;
            UpdateMonitoringState();
        }
        finally
        {
            IsSessionBusy = false;
        }
    }

    private bool CanStartSession() =>
        !IsSessionBusy && !string.IsNullOrWhiteSpace(StartSessionTitle);

    partial void OnStartSessionTitleChanged(string value)
    {
        StartSessionCommand.NotifyCanExecuteChanged();
    }

    partial void OnActiveSessionChanged(UserSession? value)
    {
        EndSessionCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsSessionBusyChanged(bool value)
    {
        StartSessionCommand.NotifyCanExecuteChanged();
        EndSessionCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanEndSession))]
    private async Task EndSessionAsync()
    {
        if (ActiveSession == null)
            return;

        IsSessionBusy = true;
        try
        {
            var endResult = await _sessionOrchestrator.EndSessionAsync();
            if (endResult == null)
                return;

            if (!string.IsNullOrWhiteSpace(endResult.ApiErrorMessage))
            {
                ApiErrorMessage = endResult.ApiErrorMessage;
                IsApiErrorVisible = true;
                return;
            }

            ActiveSession = null;
            ResetFocusState();
            UpdateMonitoringState();
        }
        finally
        {
            IsSessionBusy = false;
        }
    }

    private bool CanEndSession() => ActiveSession != null && !IsSessionBusy;

    [RelayCommand]
    private void DismissApiError()
    {
        IsApiErrorVisible = false;
        ApiErrorMessage = null;
    }

    partial void OnIsApiErrorVisibleChanged(bool value)
    {
        if (!value)
            ApiErrorMessage = null;
    }

    [RelayCommand]
    private async Task PauseSessionAsync()
    {
        if (ActiveSession == null)
            return;

        var result = await _sessionOrchestrator.PauseSessionAsync();
        if (!result.IsSuccess)
        {
            ApiErrorMessage = result.ErrorMessage ?? "Could not pause session.";
            IsApiErrorVisible = true;
            return;
        }
        OnPropertyChanged(nameof(IsSessionPaused));
        RaiseFocusOverlayStateChanged();
    }

    [RelayCommand]
    private async Task ResumeSessionAsync()
    {
        if (ActiveSession == null)
            return;

        var result = await _sessionOrchestrator.ResumeSessionAsync();
        if (!result.IsSuccess)
        {
            ApiErrorMessage = result.ErrorMessage ?? "Could not resume session.";
            IsApiErrorVisible = true;
            return;
        }
        OnPropertyChanged(nameof(IsSessionPaused));
        RaiseFocusOverlayStateChanged();
    }

    [RelayCommand]
    private void OpenSettings() => _navigationService.NavigateToSettings();

    [RelayCommand]
    private void OpenHowItWorks() => ShowHowItWorksRequested?.Invoke(this, EventArgs.Empty);

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

    /// <summary>
    /// Returns true if the user has already dismissed the trial welcome dialog.
    /// </summary>
    public async Task<bool> GetHasSeenTrialWelcomeAsync()
    {
        var value = await _settingsService.GetSettingAsync<bool>(SettingsKeys.TrialWelcomeSeen);
        return value == true;
    }

    /// <summary>
    /// Marks the trial welcome dialog as seen.
    /// </summary>
    public Task SetHasSeenTrialWelcomeAsync() =>
        _settingsService.SetSettingAsync(SettingsKeys.TrialWelcomeSeen, true);

    /// <summary>
    /// Returns true when the Foqus trial welcome should be shown (authenticated, trial tier, not yet seen).
    /// </summary>
    public async Task<bool> ShouldShowTrialWelcomeAsync()
    {
        if (!AccountSection.IsAuthenticated)
            return false;

        if (await GetHasSeenTrialWelcomeAsync())
            return false;

        await _planService.RefreshAsync();
        var plan = await _planService.GetCurrentPlanAsync();
        var status = await _planService.GetStatusAsync();
        return plan == ClientPlanType.FreeBYOK && status == ClientSubscriptionStatus.Trial;
    }

    private bool HasActiveSession() => ActiveSession != null;

    private void ResetFocusState()
    {
        CurrentFocusScorePercent = 0;
        FocusedTime = "00:00:00";
        DistractedTime = "00:00:00";
        DistractionCount = 0;
        Status.Reset();
        OnPropertyChanged(nameof(IsForegroundBrowserEdgeOrChrome));
        OnPropertyChanged(nameof(ShowExtensionPromo));
        OnPropertyChanged(nameof(IsFocusScoreVisible));
        RaiseFocusOverlayStateChanged();
    }

    private void RaiseFocusOverlayStateChanged()
    {
        var hasActive = HasActiveSession();
        var isLoading = hasActive && !Status.HasCurrentFocusResult;
        var hasError = AiRequestError != null;
        var status = Status.FocusScore switch
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
        Status.IsMonitoring = ActiveSession != null;
        OnPropertyChanged(nameof(ShowStartForm));
        OnPropertyChanged(nameof(IsActiveSessionVisible));
        OnPropertyChanged(nameof(IsFocusResultVisible));
        OnPropertyChanged(nameof(IsFocusScoreVisible));
        OnPropertyChanged(nameof(IsFocusScorePercentVisible));
        OnPropertyChanged(nameof(IsForegroundBrowserEdgeOrChrome));
        OnPropertyChanged(nameof(ShowExtensionPromo));
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

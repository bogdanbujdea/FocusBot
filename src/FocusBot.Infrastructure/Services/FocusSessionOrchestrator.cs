using FocusBot.Core.Configuration;
using FocusBot.Core.Entities;
using FocusBot.Core.Events;
using FocusBot.Core.Helpers;
using FocusBot.Core.Interfaces;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Orchestrates focus session business logic: time tracking, classification triggers,
/// idle/active handling, and backend synchronization.
/// </summary>
public sealed class FocusSessionOrchestrator : IFocusSessionOrchestrator
{
    private const int NeutralAppScore = 5;

    private readonly ILocalSessionTracker _sessionTracker;
    private readonly ISessionRepository _sessionRepository;
    private readonly IWindowMonitorService _windowMonitor;
    private readonly IClassificationService _classificationService;
    private readonly IFocusBotApiClient _apiClient;
    private readonly IAlignmentCacheRepository _alignmentCacheRepository;
    private readonly IIntegrationService? _integrationService;
    private readonly IDeviceService? _deviceService;

    private readonly object _lock = new();

    // Session state
    private UserSession? _activeSession;
    private long _sessionElapsedSeconds;
    private int _secondsSinceLastPersist;
    private bool _isSessionPaused;
    private Guid? _backendSessionId;

    // Classification state
    private string _currentProcessName = string.Empty;
    private string _currentWindowTitle = string.Empty;
    private int _focusScore;
    private string _focusReason = string.Empty;
    private bool _isClassifying;
    private bool _hasCurrentFocusResult;
    private string? _aiRequestError;
    private int _currentFocusScorePercent;

    /// <inheritdoc />
    public event EventHandler<FocusSessionStateChangedEventArgs>? StateChanged;

    /// <inheritdoc />
    public bool HasActiveSession => _activeSession != null;

    /// <inheritdoc />
    public bool IsSessionPaused => _isSessionPaused;

    /// <inheritdoc />
    public long SessionElapsedSeconds => _sessionElapsedSeconds;

    /// <inheritdoc />
    public int FocusScorePercent => _currentFocusScorePercent;

    public FocusSessionOrchestrator(
        ILocalSessionTracker sessionTracker,
        ISessionRepository sessionRepository,
        IWindowMonitorService windowMonitor,
        IClassificationService classificationService,
        IFocusBotApiClient apiClient,
        IAlignmentCacheRepository alignmentCacheRepository,
        IIntegrationService? integrationService = null,
        IDeviceService? deviceService = null
    )
    {
        _sessionTracker = sessionTracker;
        _sessionRepository = sessionRepository;
        _windowMonitor = windowMonitor;
        _classificationService = classificationService;
        _apiClient = apiClient;
        _alignmentCacheRepository = alignmentCacheRepository;
        _integrationService = integrationService;
        _deviceService = deviceService;

        _windowMonitor.Tick += OnTick;
        _windowMonitor.UserBecameIdle += OnUserBecameIdle;
        _windowMonitor.UserBecameActive += OnUserBecameActive;
        _windowMonitor.ForegroundWindowChanged += OnForegroundWindowChanged;
    }

    /// <inheritdoc />
    public void StartSession(UserSession session, long initialElapsedSeconds = 0)
    {
        lock (_lock)
        {
            _activeSession = session;
            _sessionElapsedSeconds = initialElapsedSeconds;
            _secondsSinceLastPersist = 0;
            _isSessionPaused = false;
            _currentFocusScorePercent = 0;

            // Reset classification state
            _focusScore = 0;
            _focusReason = string.Empty;
            _isClassifying = false;
            _hasCurrentFocusResult = false;
            _aiRequestError = null;

            _sessionTracker.Start(session.SessionTitle);
            _windowMonitor.Start();

            _ = StartBackendSessionAsync(session);
        }

        RaiseStateChanged();
    }

    /// <inheritdoc />
    public async Task<SessionSummary?> EndSessionAsync()
    {
        UserSession? sessionToEnd;
        SessionSummary? summary;
        Guid? backendId;

        lock (_lock)
        {
            sessionToEnd = _activeSession;
            if (sessionToEnd == null)
                return null;

            summary = _sessionTracker.GetSessionSummary();
            backendId = _backendSessionId;

            _windowMonitor.Stop();
            _sessionTracker.Reset();

            _activeSession = null;
            _sessionElapsedSeconds = 0;
            _secondsSinceLastPersist = 0;
            _isSessionPaused = false;
            _backendSessionId = null;
            _currentFocusScorePercent = 0;

            // Reset classification state
            _currentProcessName = string.Empty;
            _currentWindowTitle = string.Empty;
            _focusScore = 0;
            _focusReason = string.Empty;
            _isClassifying = false;
            _hasCurrentFocusResult = false;
            _aiRequestError = null;
        }

        // Persist final state
        await _sessionRepository.UpdateFocusScoreAsync(
            sessionToEnd.SessionId,
            summary.FocusScorePercent
        );
        await _sessionRepository.SetCompletedAsync(sessionToEnd.SessionId);

        // Notify backend
        if (backendId.HasValue)
        {
            var payload = new EndSessionPayload(
                summary.FocusScorePercent,
                summary.FocusedSeconds,
                summary.DistractedSeconds,
                summary.DistractionCount,
                summary.ContextSwitchCount,
                summary.TopDistractingApps,
                summary.TopAlignedApps,
                _deviceService?.GetDeviceId()
            );
            _ = _apiClient.EndSessionAsync(backendId.Value, payload);
        }

        RaiseStateChanged();
        return summary;
    }

    /// <inheritdoc />
    public void PauseSession()
    {
        lock (_lock)
        {
            if (_activeSession == null)
                return;

            _isSessionPaused = true;
            _sessionTracker.HandleIdle(true);
            _windowMonitor.Stop();
        }

        RaiseStateChanged();
    }

    /// <inheritdoc />
    public void ResumeSession()
    {
        lock (_lock)
        {
            if (_activeSession == null)
                return;

            _isSessionPaused = false;
            _sessionTracker.HandleIdle(false);
            _windowMonitor.Start();
        }

        RaiseStateChanged();
    }

    /// <inheritdoc />
    public async Task SyncBackendSessionAsync()
    {
        if (!_apiClient.IsConfigured)
            return;

        var activeSession = await _apiClient.GetActiveSessionAsync();
        if (activeSession != null)
        {
            lock (_lock)
            {
                _backendSessionId = activeSession.Id;
            }
        }
    }

    /// <inheritdoc />
    public async Task RecordManualOverrideAsync(int newScore, string newReason)
    {
        UserSession? session;
        string processName;
        string windowTitle;

        lock (_lock)
        {
            session = _activeSession;
            if (session == null)
                return;

            processName = _currentProcessName;
            windowTitle = _currentWindowTitle;
        }

        var contextHash = HashHelper.ComputeWindowContextHash(processName, windowTitle);
        var sessionContentHash = HashHelper.ComputeSessionContentHash(
            session.SessionTitle,
            session.Context
        );

        var entry = new AlignmentCacheEntry
        {
            ContextHash = contextHash,
            TaskContentHash = sessionContentHash,
            Score = newScore,
            Reason = newReason,
            CreatedAt = DateTime.UtcNow,
        };

        await _alignmentCacheRepository.SaveAsync(entry);

        lock (_lock)
        {
            _focusScore = newScore;
            _focusReason = newReason;
            _sessionTracker.RecordClassification(
                processName,
                new AlignmentResult { Score = newScore, Reason = newReason }
            );
        }

        RaiseStateChanged();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        string? sessionId;

        lock (_lock)
        {
            if (_activeSession == null)
                return;

            sessionId = _activeSession.SessionId;
            _sessionElapsedSeconds++;

            _sessionTracker.RecordTick();
            _currentFocusScorePercent = _sessionTracker.GetFocusScore();

            _secondsSinceLastPersist++;
        }

        RaiseStateChanged();

        // Persist periodically (outside lock to avoid holding it during async)
        int secondsSincePersist;
        lock (_lock)
        {
            secondsSincePersist = _secondsSinceLastPersist;
            if (secondsSincePersist >= FocusSessionConfig.PersistIntervalSeconds)
            {
                _secondsSinceLastPersist = 0;
            }
        }

        if (secondsSincePersist >= FocusSessionConfig.PersistIntervalSeconds && sessionId != null)
        {
            _ = PersistElapsedTimeAsync(sessionId);
        }
    }

    private void OnUserBecameIdle(object? sender, EventArgs e)
    {
        lock (_lock)
        {
            if (_activeSession == null)
                return;

            var backdateSeconds = (int)_windowMonitor.IdleThreshold.TotalSeconds;
            _sessionElapsedSeconds = Math.Max(0L, _sessionElapsedSeconds - backdateSeconds);

            _sessionTracker.HandleIdle(true);
            _windowMonitor.Stop();
        }

        RaiseStateChanged();
    }

    private void OnUserBecameActive(object? sender, EventArgs e)
    {
        lock (_lock)
        {
            if (_activeSession == null)
                return;

            _sessionTracker.HandleIdle(false);
            _windowMonitor.Start();
        }

        RaiseStateChanged();
    }

    private void OnForegroundWindowChanged(object? sender, ForegroundWindowChangedEventArgs e)
    {
        UserSession? session;
        string sessionTitle;
        string? sessionContext;

        lock (_lock)
        {
            _currentProcessName = e.ProcessName;
            _currentWindowTitle = e.WindowTitle;

            session = _activeSession;

            if (session == null)
            {
                // No active session - just notify extension about foreground
                if (_integrationService is { IsExtensionConnected: true })
                {
                    _ = _integrationService.SendDesktopForegroundAsync(
                        e.ProcessName,
                        e.WindowTitle
                    );
                }

                _focusScore = 0;
                _focusReason = string.Empty;
                _isClassifying = false;
                _hasCurrentFocusResult = false;
                RaiseStateChanged();
                return;
            }

            sessionTitle = session.SessionTitle;
            sessionContext = session.Context;

            // Check for neutral app
            if (IsNeutralApp(e.ProcessName))
            {
                _focusScore = NeutralAppScore;
                _focusReason = "You are visiting a neutral app";
                _isClassifying = false;
                _hasCurrentFocusResult = true;
                RaiseStateChanged();
                return;
            }

            // Reset state before classification
            _focusScore = 0;
            _focusReason = string.Empty;
            _isClassifying = false;
            _hasCurrentFocusResult = false;
        }

        RaiseStateChanged();

        // Notify extension about foreground
        if (_integrationService is { IsExtensionConnected: true })
        {
            _ = _integrationService.SendDesktopForegroundAsync(e.ProcessName, e.WindowTitle);
        }

        // Trigger classification
        if (
            BrowserProcessNames.IsBrowser(e.ProcessName)
            && _integrationService is { IsExtensionConnected: true }
        )
        {
            _ = ClassifyWithBrowserContextAsync(
                sessionTitle,
                sessionContext,
                e.ProcessName,
                e.WindowTitle
            );
        }
        else
        {
            _ = ClassifyAndUpdateFocusAsync(
                sessionTitle,
                sessionContext,
                e.ProcessName,
                e.WindowTitle
            );
        }
    }

    private async Task ClassifyAndUpdateFocusAsync(
        string sessionTitle,
        string? sessionContext,
        string processName,
        string windowTitle
    )
    {
        lock (_lock)
        {
            _isClassifying = true;
            _aiRequestError = null;
        }
        RaiseStateChanged();

        try
        {
            var result = await _classificationService.ClassifyAsync(
                processName,
                windowTitle,
                sessionTitle,
                sessionContext
            );

            lock (_lock)
            {
                if (result.IsFailure)
                {
                    _aiRequestError = result.Error;
                }
                else
                {
                    _focusScore = result.Value.Score;
                    _focusReason = result.Value.Reason;
                    _sessionTracker.RecordClassification(processName, result.Value);
                    _aiRequestError = null;
                    _hasCurrentFocusResult = true;
                    _currentFocusScorePercent = _sessionTracker.GetFocusScore();
                }
            }
        }
        finally
        {
            lock (_lock)
            {
                _isClassifying = false;
            }
            RaiseStateChanged();
        }
    }

    private async Task ClassifyWithBrowserContextAsync(
        string sessionTitle,
        string? sessionContext,
        string processName,
        string windowTitle
    )
    {
        if (_integrationService is not { IsExtensionConnected: true })
        {
            await ClassifyAndUpdateFocusAsync(
                sessionTitle,
                sessionContext,
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

            lock (_lock)
            {
                _currentWindowTitle = $"Browser: {domain}";
            }

            var combinedTitle = $"{browserContext.Title} ({browserContext.Url})";
            await ClassifyAndUpdateFocusAsync(
                sessionTitle,
                sessionContext,
                processName,
                combinedTitle
            );
        }
        else
        {
            await ClassifyAndUpdateFocusAsync(
                sessionTitle,
                sessionContext,
                processName,
                windowTitle
            );
        }
    }

    private async Task StartBackendSessionAsync(UserSession session)
    {
        if (!_apiClient.IsConfigured)
            return;

        var deviceId = _deviceService?.GetDeviceId();
        var payload = new StartSessionPayload(session.SessionTitle, session.Context, deviceId);
        var response = await _apiClient.StartSessionAsync(payload);

        if (response != null)
        {
            lock (_lock)
            {
                _backendSessionId = response.Id;
            }
            return;
        }

        // StartSession failed — likely 409 Conflict (active session already exists).
        // End the stale session and retry creating a new one.
        var staleSession = await _apiClient.GetActiveSessionAsync();
        if (staleSession == null)
            return;

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

        var retryResponse = await _apiClient.StartSessionAsync(payload);
        if (retryResponse != null)
        {
            lock (_lock)
            {
                _backendSessionId = retryResponse.Id;
            }
        }
    }

    private async Task PersistElapsedTimeAsync(string sessionId)
    {
        long elapsed;
        lock (_lock)
        {
            elapsed = _sessionElapsedSeconds;
        }
        await _sessionRepository.UpdateElapsedTimeAsync(sessionId, elapsed);
    }

    private static bool IsNeutralApp(string processName)
    {
        return processName == "Foqus"
            || processName == "explorer"
            || processName == "StartMenuExperienceHost"
            || processName == "ApplicationFrameHost"
            || processName == "ShellExperienceHost";
    }

    private void RaiseStateChanged()
    {
        string processName;
        string windowTitle;
        long elapsed;
        int focusScorePercent;
        bool isClassifying;
        int focusScore;
        string focusReason;
        bool hasResult;
        bool isPaused;
        string? error;

        lock (_lock)
        {
            processName = _currentProcessName;
            windowTitle = _currentWindowTitle;
            elapsed = _sessionElapsedSeconds;
            focusScorePercent = _currentFocusScorePercent;
            isClassifying = _isClassifying;
            focusScore = _focusScore;
            focusReason = _focusReason;
            hasResult = _hasCurrentFocusResult;
            isPaused = _isSessionPaused;
            error = _aiRequestError;
        }

        StateChanged?.Invoke(
            this,
            new FocusSessionStateChangedEventArgs
            {
                SessionElapsedSeconds = elapsed,
                FocusScorePercent = focusScorePercent,
                IsClassifying = isClassifying,
                FocusScore = focusScore,
                FocusReason = focusReason,
                HasCurrentFocusResult = hasResult,
                IsSessionPaused = isPaused,
                AiRequestError = error,
                CurrentProcessName = processName,
                CurrentWindowTitle = windowTitle,
            }
        );
    }
}

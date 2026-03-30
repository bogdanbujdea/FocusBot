using System.Net;
using FocusBot.Core.Configuration;
using FocusBot.Core.Entities;
using FocusBot.Core.Events;
using FocusBot.Core.Interfaces;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Orchestrates focus session business logic: time tracking, classification triggers,
/// idle/active handling, and Web API session lifecycle.
/// </summary>
public sealed class FocusSessionOrchestrator : IFocusSessionOrchestrator
{
    private const int NeutralAppScore = 5;

    private readonly ILocalSessionTracker _sessionTracker;
    private readonly IWindowMonitorService _windowMonitor;
    private readonly IClassificationService _classificationService;
    private readonly IFocusBotApiClient _apiClient;
    private readonly IClientService? _clientService;
    private readonly IExtensionPresenceService? _extensionPresence;

    private readonly object _lock = new();

    // Session state
    private UserSession? _activeSession;
    private long _sessionElapsedSeconds;
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
        IWindowMonitorService windowMonitor,
        IClassificationService classificationService,
        IFocusBotApiClient apiClient,
        IClientService? clientService = null,
        IExtensionPresenceService? extensionPresence = null
    )
    {
        _sessionTracker = sessionTracker;
        _windowMonitor = windowMonitor;
        _classificationService = classificationService;
        _apiClient = apiClient;
        _clientService = clientService;
        _extensionPresence = extensionPresence;

        _windowMonitor.Tick += OnTick;
        _windowMonitor.UserBecameIdle += OnUserBecameIdle;
        _windowMonitor.UserBecameActive += OnUserBecameActive;
        _windowMonitor.ForegroundWindowChanged += OnForegroundWindowChanged;
    }

    /// <inheritdoc />
    public async Task<ApiResult<UserSession>> StartSessionAsync(string sessionTitle, string? sessionContext)
    {
        if (!_apiClient.IsConfigured)
            return ApiResult<UserSession>.NotAuthenticated();

        var clientId = _clientService?.GetClientId();
        var payload = new StartSessionPayload(sessionTitle, sessionContext, clientId);
        var result = await _apiClient.StartSessionAsync(payload);

        if (result.IsSuccess && result.Value != null)
        {
            var session = UserSession.FromApiResponse(result.Value);
            BeginLocalSessionTracking(session, 0);
            return ApiResult<UserSession>.Success(session);
        }

        if (result.StatusCode == HttpStatusCode.Conflict)
            return await TryResolveConflictAndStartAsync(payload, clientId);

        return ApiResultMappings.FromFailedSessionCall<UserSession>(result);
    }

    /// <inheritdoc />
    public void BeginLocalSessionTracking(UserSession session, long initialElapsedSeconds = 0)
    {
        lock (_lock)
        {
            _activeSession = session;
            _backendSessionId = Guid.TryParse(session.SessionId, out var sid) ? sid : null;
            _sessionElapsedSeconds = initialElapsedSeconds;
            _isSessionPaused = false;
            _currentFocusScorePercent = 0;

            _focusScore = 0;
            _focusReason = string.Empty;
            _isClassifying = false;
            _hasCurrentFocusResult = false;
            _aiRequestError = null;

            _sessionTracker.Start(session.SessionTitle);
            _windowMonitor.Start();
        }

        RaiseStateChanged();
    }

    /// <inheritdoc />
    public async Task<UserSession?> LoadActiveSessionAsync()
    {
        if (!_apiClient.IsConfigured)
            return null;

        var api = await _apiClient.GetActiveSessionAsync();
        return api == null ? null : UserSession.FromApiResponse(api);
    }

    /// <inheritdoc />
    public void StopLocalTrackingIfActive()
    {
        lock (_lock)
        {
            if (_activeSession == null)
                return;

            _windowMonitor.Stop();
            _sessionTracker.Reset();

            _activeSession = null;
            _sessionElapsedSeconds = 0;
            _isSessionPaused = false;
            _backendSessionId = null;
            _currentFocusScorePercent = 0;

            _currentProcessName = string.Empty;
            _currentWindowTitle = string.Empty;
            _focusScore = 0;
            _focusReason = string.Empty;
            _isClassifying = false;
            _hasCurrentFocusResult = false;
            _aiRequestError = null;
        }

        RaiseStateChanged();
    }

    /// <inheritdoc />
    public async Task<SessionEndResult?> EndSessionAsync()
    {
        UserSession? sessionToEnd;
        SessionSummary summary;
        Guid? backendId;

        lock (_lock)
        {
            sessionToEnd = _activeSession;
            if (sessionToEnd == null)
                return null;

            summary = _sessionTracker.GetSessionSummary();
            backendId = _backendSessionId;
        }

        if (!backendId.HasValue)
        {
            return new SessionEndResult
            {
                Summary = summary,
                ApiErrorMessage = "Session is not linked to the server.",
            };
        }

        var payload = new EndSessionPayload(
            summary.FocusScorePercent,
            summary.FocusedSeconds,
            summary.DistractedSeconds,
            summary.DistractionCount,
            summary.ContextSwitchCount,
            summary.TopDistractingApps,
            summary.TopAlignedApps,
            _clientService?.GetClientId()
        );

        var endResult = await _apiClient.EndSessionAsync(backendId.Value, payload);
        if (!endResult.IsSuccess)
        {
            return new SessionEndResult
            {
                Summary = summary,
                ApiErrorMessage = endResult.ErrorMessage,
            };
        }

        StopLocalTrackingIfActive();

        return new SessionEndResult
        {
            Summary = summary,
            ApiErrorMessage = null,
        };
    }

    /// <inheritdoc />
    public async Task<ApiResult<bool>> PauseSessionAsync()
    {
        Guid? backendId;
        lock (_lock)
        {
            if (_activeSession == null)
                return ApiResult<bool>.Failure(HttpStatusCode.NotFound);

            backendId = _backendSessionId;
        }

        if (!backendId.HasValue)
            return ApiResult<bool>.Failure(HttpStatusCode.Conflict);

        var result = await _apiClient.PauseSessionAsync(backendId.Value);
        if (!result.IsSuccess)
            return ApiResult<bool>.Failure(result.StatusCode ?? HttpStatusCode.InternalServerError);

        lock (_lock)
        {
            if (_activeSession == null)
                return ApiResult<bool>.Failure(HttpStatusCode.NotFound);
            _isSessionPaused = true;
            _sessionTracker.HandleIdle(true);
            _windowMonitor.Stop();
        }

        RaiseStateChanged();
        return ApiResult<bool>.Success(true);
    }

    /// <inheritdoc />
    public async Task<ApiResult<bool>> ResumeSessionAsync()
    {
        Guid? backendId;
        lock (_lock)
        {
            if (_activeSession == null)
                return ApiResult<bool>.Failure(HttpStatusCode.NotFound);

            backendId = _backendSessionId;
        }

        if (!backendId.HasValue)
            return ApiResult<bool>.Failure(HttpStatusCode.Conflict);

        var result = await _apiClient.ResumeSessionAsync(backendId.Value);
        if (!result.IsSuccess)
            return ApiResult<bool>.Failure(result.StatusCode ?? HttpStatusCode.InternalServerError);

        lock (_lock)
        {
            if (_activeSession == null)
                return ApiResult<bool>.Failure(HttpStatusCode.NotFound);
            _isSessionPaused = false;
            _sessionTracker.HandleIdle(false);
            _windowMonitor.Start();
        }

        RaiseStateChanged();
        return ApiResult<bool>.Success(true);
    }

    /// <inheritdoc />
    public void ApplyRemotePause()
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
    public void ApplyRemoteClassificationFromHub(
        string source,
        int score,
        string reason,
        string activityName)
    {
        if (string.Equals(source, "desktop", StringComparison.OrdinalIgnoreCase))
            return;

        lock (_lock)
        {
            if (_activeSession == null || _isSessionPaused)
                return;

            _focusScore = score;
            _focusReason = reason;
            _hasCurrentFocusResult = true;
            _isClassifying = false;
            _aiRequestError = null;

            if (!string.IsNullOrWhiteSpace(activityName))
                _currentWindowTitle = activityName;

            var processKey = string.IsNullOrWhiteSpace(_currentProcessName)
                ? "browser"
                : _currentProcessName;

            _sessionTracker.RecordClassification(
                processKey,
                new AlignmentResult { Score = score, Reason = reason });
            _currentFocusScorePercent = _sessionTracker.GetFocusScore();
        }

        RaiseStateChanged();
    }

    /// <inheritdoc />
    public void ApplyRemoteResume()
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

    private async Task<ApiResult<UserSession>> TryResolveConflictAndStartAsync(
        StartSessionPayload payload,
        Guid? clientId
    )
    {
        var staleSession = await _apiClient.GetActiveSessionAsync();
        if (staleSession == null)
            return ApiResult<UserSession>.NetworkError();

        var abandonPayload = new EndSessionPayload(
            FocusScorePercent: 0,
            FocusedSeconds: 0,
            DistractedSeconds: 0,
            DistractionCount: 0,
            ContextSwitchCount: 0,
            TopDistractingApps: null,
            TopAlignedApps: null,
            ClientId: clientId
        );
        var abandonResult = await _apiClient.EndSessionAsync(staleSession.Id, abandonPayload);
        if (!abandonResult.IsSuccess)
            return ApiResultMappings.FromFailedSessionCall<UserSession>(abandonResult);

        var retryResult = await _apiClient.StartSessionAsync(payload);
        if (retryResult.IsSuccess && retryResult.Value != null)
        {
            var session = UserSession.FromApiResponse(retryResult.Value);
            BeginLocalSessionTracking(session, 0);
            return ApiResult<UserSession>.Success(session);
        }

        return ApiResultMappings.FromFailedSessionCall<UserSession>(retryResult);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        lock (_lock)
        {
            if (_activeSession == null)
                return;

            _sessionElapsedSeconds++;

            _sessionTracker.RecordTick();
            _currentFocusScorePercent = _sessionTracker.GetFocusScore();
        }

        RaiseStateChanged();
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
                _focusScore = 0;
                _focusReason = string.Empty;
                _isClassifying = false;
                _hasCurrentFocusResult = false;
                RaiseStateChanged();
                return;
            }

            sessionTitle = session.SessionTitle;
            sessionContext = session.Context;

            if (IsNeutralApp(e.ProcessName))
            {
                _focusScore = NeutralAppScore;
                _focusReason = "You are visiting a neutral app";
                _isClassifying = false;
                _hasCurrentFocusResult = true;
                RaiseStateChanged();
                return;
            }

            if (IsBrowserProcess(e.ProcessName) && _extensionPresence?.IsExtensionOnline == true)
            {
                _focusScore = 5;
                _focusReason = "Browser activity tracked by extension";
                _isClassifying = false;
                _hasCurrentFocusResult = true;
                RaiseStateChanged();
                return;
            }

            _focusScore = 0;
            _focusReason = string.Empty;
            _isClassifying = false;
            _hasCurrentFocusResult = false;
        }

        RaiseStateChanged();

        _ = ClassifyAndUpdateFocusAsync(
            sessionTitle,
            sessionContext,
            e.ProcessName,
            e.WindowTitle
        );
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

    private static bool IsNeutralApp(string processName)
    {
        return processName == "Foqus"
            || processName == "explorer"
            || processName == "Taskmgr"
            || processName == "StartMenuExperienceHost"
            || processName == "ApplicationFrameHost"
            || processName == "ShellExperienceHost";
    }

    private static bool IsBrowserProcess(string processName)
    {
        return processName.Equals("msedge", StringComparison.OrdinalIgnoreCase)
            || processName.Equals("chrome", StringComparison.OrdinalIgnoreCase)
            || processName.Equals("firefox", StringComparison.OrdinalIgnoreCase)
            || processName.Equals("brave", StringComparison.OrdinalIgnoreCase)
            || processName.Equals("opera", StringComparison.OrdinalIgnoreCase);
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
        long focusedSeconds;
        long distractedSeconds;
        int distractionCount;

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
            focusedSeconds = _sessionTracker.GetFocusedSeconds();
            distractedSeconds = _sessionTracker.GetDistractedSeconds();
            distractionCount = _sessionTracker.GetDistractionCount();
        }

        StateChanged?.Invoke(
            this,
            new FocusSessionStateChangedEventArgs
            {
                SessionElapsedSeconds = elapsed,
                FocusScorePercent = focusScorePercent,
                FocusedSeconds = focusedSeconds,
                DistractedSeconds = distractedSeconds,
                DistractionCount = distractionCount,
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

using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Central coordinator for session lifecycle state and API orchestration.
/// Serializes all session mutations to prevent race conditions.
/// Controls foreground classification based on session state.
/// </summary>
public class SessionCoordinator : ISessionCoordinator
{
    private readonly IFocusBotApiClient _apiClient;
    private readonly IForegroundClassificationCoordinator _classificationCoordinator;
    private readonly ILogger<SessionCoordinator> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private SessionState _currentState = SessionState.Initial();

    public SessionState CurrentState => _currentState;

    public event Action<SessionState, SessionChangeType>? StateChanged;

    public SessionCoordinator(
        IFocusBotApiClient apiClient,
        IForegroundClassificationCoordinator classificationCoordinator,
        ILogger<SessionCoordinator> logger
    )
    {
        _apiClient = apiClient;
        _classificationCoordinator = classificationCoordinator;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _logger.LogInformation("Initializing session coordinator");
            var existingSession = await _apiClient.GetActiveSessionAsync();
            if (existingSession is not null)
            {
                _logger.LogInformation(
                    "Loaded existing active session: {SessionId}",
                    existingSession.Id
                );
                UpdateState(existingSession, null, SessionChangeType.Synced);

                if (!existingSession.IsPaused)
                    _classificationCoordinator.Start(
                        existingSession.SessionTitle,
                        existingSession.SessionContext
                    );
            }
            else
            {
                _logger.LogInformation("No existing active session");
                UpdateState(null, null, SessionChangeType.Started);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize session coordinator");
            UpdateState(null, "Failed to load session", SessionChangeType.Failed);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> StartAsync(string title, string? context)
    {
        await _lock.WaitAsync();
        try
        {
            if (_currentState.HasActiveSession)
            {
                _logger.LogWarning("Cannot start session: active session already exists");
                UpdateState(
                    _currentState.ActiveSession,
                    "Active session already exists",
                    SessionChangeType.Failed
                );
                return false;
            }

            _logger.LogInformation("Starting session: {Title}", title);

            var payload = new StartSessionPayload(title.Trim(), context?.Trim());
            var result = await _apiClient.StartSessionAsync(payload);

            if (result is { IsSuccess: true, Value: not null })
            {
                _logger.LogInformation(
                    "Session started successfully: {SessionId}",
                    result.Value.Id
                );
                UpdateState(result.Value, null, SessionChangeType.Started);
                _classificationCoordinator.Start(
                    result.Value.SessionTitle,
                    result.Value.SessionContext
                );
                return true;
            }
            else
            {
                var errorMessage = result.ErrorMessage ?? "Failed to start session";
                _logger.LogWarning("Failed to start session: {Error}", errorMessage);
                UpdateState(null, errorMessage, SessionChangeType.Failed);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while starting session");
            UpdateState(null, ex.Message, SessionChangeType.Failed);
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> PauseAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!_currentState.HasActiveSession)
            {
                _logger.LogWarning("Cannot pause: no active session");
                return false;
            }

            var sessionId = _currentState.ActiveSession!.Id;
            _logger.LogInformation("Pausing session: {SessionId}", sessionId);

            var result = await _apiClient.PauseSessionAsync(sessionId);

            if (result is { IsSuccess: true, Value: not null })
            {
                _logger.LogInformation("Session paused successfully: {SessionId}", sessionId);
                UpdateState(result.Value, null, SessionChangeType.Paused);
                _classificationCoordinator.Stop();
                return true;
            }
            else
            {
                var errorMessage = result.ErrorMessage ?? "Failed to pause session";
                _logger.LogWarning("Failed to pause session: {Error}", errorMessage);
                UpdateState(_currentState.ActiveSession, errorMessage, SessionChangeType.Failed);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while pausing session");
            UpdateState(_currentState.ActiveSession, ex.Message, SessionChangeType.Failed);
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> ResumeAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!_currentState.HasActiveSession)
            {
                _logger.LogWarning("Cannot resume: no active session");
                return false;
            }

            var sessionId = _currentState.ActiveSession!.Id;
            _logger.LogInformation("Resuming session: {SessionId}", sessionId);

            var result = await _apiClient.ResumeSessionAsync(sessionId);

            if (result is { IsSuccess: true, Value: not null })
            {
                _logger.LogInformation("Session resumed successfully: {SessionId}", sessionId);
                UpdateState(result.Value, null, SessionChangeType.Resumed);
                _classificationCoordinator.Start(
                    result.Value.SessionTitle,
                    result.Value.SessionContext
                );
                return true;
            }
            else
            {
                var errorMessage = result.ErrorMessage ?? "Failed to resume session";
                _logger.LogWarning("Failed to resume session: {Error}", errorMessage);
                UpdateState(_currentState.ActiveSession, errorMessage, SessionChangeType.Failed);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while resuming session");
            UpdateState(_currentState.ActiveSession, ex.Message, SessionChangeType.Failed);
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> StopAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!_currentState.HasActiveSession)
            {
                _logger.LogWarning("Cannot stop: no active session");
                return false;
            }

            var sessionId = _currentState.ActiveSession!.Id;
            _logger.LogInformation("Stopping session: {SessionId}", sessionId);

            var payload = new EndSessionPayload(0, 0, 0, 0, 0, null, null);
            var result = await _apiClient.EndSessionAsync(sessionId, payload);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Session stopped successfully: {SessionId}", sessionId);
                UpdateState(null, null, SessionChangeType.Stopped);
                _classificationCoordinator.Stop();
                return true;
            }
            else
            {
                var errorMessage = result.ErrorMessage ?? "Failed to stop session";
                _logger.LogWarning("Failed to stop session: {Error}", errorMessage);

                if (result.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    _logger.LogInformation("Session already ended (409), reconciling state");
                    UpdateState(null, null, SessionChangeType.Stopped);
                    _classificationCoordinator.Stop();
                    return true;
                }

                UpdateState(_currentState.ActiveSession, errorMessage, SessionChangeType.Failed);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while stopping session");
            UpdateState(_currentState.ActiveSession, ex.Message, SessionChangeType.Failed);
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ApplyRemoteSessionStartedAsync(SessionStartedEvent evt)
    {
        await _lock.WaitAsync();
        try
        {
            if (_currentState.HasActiveSession && _currentState.ActiveSession!.Id == evt.SessionId)
            {
                _logger.LogDebug(
                    "Ignoring duplicate SessionStarted event for existing session {SessionId}",
                    evt.SessionId
                );
                return;
            }

            var activeSession = await _apiClient.GetActiveSessionAsync();
            if (activeSession is null)
            {
                _logger.LogWarning(
                    "SessionStarted event received for {SessionId} but API returned no active session",
                    evt.SessionId
                );
                return;
            }

            if (activeSession.Id != evt.SessionId)
            {
                _logger.LogWarning(
                    "SessionStarted event ID mismatch. Event {EventSessionId}, API {ApiSessionId}",
                    evt.SessionId,
                    activeSession.Id
                );
                return;
            }

            _logger.LogInformation(
                "Applied remote SessionStarted event for session {SessionId}",
                evt.SessionId
            );
            UpdateState(activeSession, null, SessionChangeType.Synced);

            if (!activeSession.IsPaused)
                _classificationCoordinator.Start(
                    activeSession.SessionTitle,
                    activeSession.SessionContext
                );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply remote SessionStarted event");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ApplyRemoteSessionEndedAsync(SessionEndedEvent evt)
    {
        await _lock.WaitAsync();
        try
        {
            var activeSession = await _apiClient.GetActiveSessionAsync();

            if (activeSession is null)
            {
                _logger.LogInformation(
                    "Applied remote SessionEnded event for session {SessionId} (no active session)",
                    evt.SessionId
                );
                UpdateState(null, null, SessionChangeType.Stopped);
                _classificationCoordinator.Stop();
                return;
            }

            _logger.LogInformation(
                "SessionEnded event for {EventSessionId}; API active session is {ApiSessionId}",
                evt.SessionId,
                activeSession.Id
            );
            UpdateState(activeSession, null, SessionChangeType.Synced);

            if (activeSession.IsPaused)
                _classificationCoordinator.Stop();
            else
                _classificationCoordinator.Start(
                    activeSession.SessionTitle,
                    activeSession.SessionContext
                );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply remote SessionEnded event");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ApplyRemoteSessionPausedAsync(SessionPausedEvent evt)
    {
        await _lock.WaitAsync();
        try
        {
            var activeSession = await _apiClient.GetActiveSessionAsync();
            if (activeSession is null || activeSession.Id != evt.SessionId)
            {
                _logger.LogDebug(
                    "Ignoring remote SessionPaused for {EventSessionId}; API active is {ApiSessionId}",
                    evt.SessionId,
                    activeSession?.Id
                );
                return;
            }

            _logger.LogInformation(
                "Applied remote SessionPaused event for session {SessionId}",
                evt.SessionId
            );
            UpdateState(activeSession, null, SessionChangeType.Paused);
            _classificationCoordinator.Stop();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply remote SessionPaused event");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ApplyRemoteSessionResumedAsync(SessionResumedEvent evt)
    {
        await _lock.WaitAsync();
        try
        {
            var activeSession = await _apiClient.GetActiveSessionAsync();
            if (activeSession is null || activeSession.Id != evt.SessionId)
            {
                _logger.LogDebug(
                    "Ignoring remote SessionResumed for {EventSessionId}; API active is {ApiSessionId}",
                    evt.SessionId,
                    activeSession?.Id
                );
                return;
            }

            _logger.LogInformation(
                "Applied remote SessionResumed event for session {SessionId}",
                evt.SessionId
            );
            UpdateState(activeSession, null, SessionChangeType.Resumed);
            if (!activeSession.IsPaused)
                _classificationCoordinator.Start(
                    activeSession.SessionTitle,
                    activeSession.SessionContext
                );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply remote SessionResumed event");
        }
        finally
        {
            _lock.Release();
        }
    }

    public void ClearError()
    {
        if (_currentState.HasError)
        {
            UpdateState(_currentState.ActiveSession, null, _currentState.LastChangeType);
        }
    }

    public void Reset()
    {
        _logger.LogInformation("Resetting session coordinator");
        _classificationCoordinator.Stop();
        UpdateState(null, null, SessionChangeType.Started);
    }

    private void UpdateState(
        ApiSessionResponse? session,
        string? errorMessage,
        SessionChangeType changeType
    )
    {
        _currentState = new SessionState(session, errorMessage, changeType);
        StateChanged?.Invoke(_currentState, changeType);
    }
}

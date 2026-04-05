# SignalR Integration Plan (Phase 2)

## Overview
Implement real-time session synchronization for the desktop app by creating a SignalR adapter that connects to the FocusBot WebAPI hub and reconciles remote session events with the local `SessionCoordinator`.

## Current implementation status (April 2026)
- Implemented: desktop subscribes to `SessionStarted` and reconciles active session state from API.
- Implemented: adapter connect/disconnect is tied to auth state in `App.xaml.cs`.
- Pending: remote handling for `SessionEnded`, `SessionPaused`, and `SessionResumed`.

---

## Current State

### Existing Infrastructure
- **WebAPI Hub**: `FocusHub` at `/hubs/focus` emits 4 session events:
  - `SessionStarted(SessionStartedEvent)` - when any client starts a session
  - `SessionEnded(SessionEndedEvent)` - when any client ends a session
  - `SessionPaused(SessionPausedEvent)` - when any client pauses a session
  - `SessionResumed(SessionResumedEvent)` - when any client resumes a session
- **Desktop Package**: `Microsoft.AspNetCore.SignalR.Client` v10.0.5 already referenced in `FocusBot.Infrastructure.csproj`
- **Authentication**: Hub requires JWT Bearer token (same as API client)
- **User Grouping**: Server automatically adds connections to per-user groups so all devices for the same user receive events

### SessionCoordinator Contract
```csharp
public interface ISessionCoordinator
{
    SessionState CurrentState { get; }
    event Action<SessionState, SessionChangeType>? StateChanged;
    
    Task InitializeAsync();
    Task<bool> StartAsync(string title, string? context);
    Task<bool> PauseAsync();
    Task<bool> ResumeAsync();
    Task<bool> StopAsync();
    void ClearError();
    void Reset();
}
```

### SessionState
```csharp
public sealed record SessionState(
    ApiSessionResponse? ActiveSession,
    string? ErrorMessage,
    SessionChangeType LastChangeType
)
```

### SessionChangeType
```csharp
public enum SessionChangeType
{
    Started,   // Session was started (or coordinator initialized with no session)
    Paused,    // Session was paused
    Resumed,   // Session was resumed
    Stopped,   // Session was stopped
    Failed,    // Operation failed
    Synced     // Existing session loaded from API
}
```

---

## Design Goals

1. **Single state owner**: SessionCoordinator remains the only component that mutates UI-facing session state
2. **Conflict-free sync**: Local user actions take precedence; remote events reconcile silently
3. **Clean separation**: SignalR logic lives in adapter, not coordinator
4. **Testable**: Adapter behavior can be unit tested independently
5. **Auth lifecycle aware**: Connect/disconnect tied to authentication state

---

## Implementation Plan

### 1. Create SignalR Event DTOs in Core Layer

**File**: `src/FocusBot.Core/Entities/SessionEvents.cs`

Map WebAPI hub events to client-side DTOs:

```csharp
public sealed record SessionStartedEvent(
    Guid SessionId,
    string SessionTitle,
    string? SessionContext,
    DateTime StartedAtUtc,
    string Source
);

public sealed record SessionEndedEvent(
    Guid SessionId,
    DateTime EndedAtUtc,
    string Source
);

public sealed record SessionPausedEvent(
    Guid SessionId,
    DateTime PausedAtUtc,
    string Source
);

public sealed record SessionResumedEvent(
    Guid SessionId,
    string Source
);
```

### 2. Add Reconciliation Methods to ISessionCoordinator

**File**: `src/FocusBot.Core/Interfaces/ISessionCoordinator.cs`

Add internal reconciliation methods for the adapter to call:

```csharp
/// <summary>
/// Apply a remote session start event from SignalR.
/// Ignores if a local session with the same ID already exists.
/// </summary>
Task ApplyRemoteSessionStartedAsync(SessionStartedEvent evt);

/// <summary>
/// Apply a remote session end event from SignalR.
/// Clears local session if IDs match.
/// </summary>
Task ApplyRemoteSessionEndedAsync(SessionEndedEvent evt);

/// <summary>
/// Apply a remote pause event from SignalR.
/// Updates local session if IDs match.
/// </summary>
Task ApplyRemoteSessionPausedAsync(SessionPausedEvent evt);

/// <summary>
/// Apply a remote resume event from SignalR.
/// Updates local session if IDs match.
/// </summary>
Task ApplyRemoteSessionResumedAsync(SessionResumedEvent evt);
```

**Implementation strategy**:
- Use the same `_lock` semaphore as other coordinator methods
- Check if event session ID matches current session ID
- Ignore events from same source (`Source == "desktop"`) to prevent echo
- Fetch full session details from API if needed (events are lightweight, session state is in DB)

### 3. Implement SignalRSessionRealtimeAdapter

**File**: `src/FocusBot.Infrastructure/Services/SignalRSessionRealtimeAdapter.cs`

```csharp
public class SignalRSessionRealtimeAdapter : ISessionRealtimeAdapter, IAsyncDisposable
{
    private readonly ISessionCoordinator _coordinator;
    private readonly IAuthService _authService;
    private readonly ILogger<SignalRSessionRealtimeAdapter> _logger;
    private readonly string _hubUrl;
    
    private HubConnection? _connection;
    private bool _disposed;

    public SignalRSessionRealtimeAdapter(
        ISessionCoordinator coordinator,
        IAuthService authService,
        ILogger<SignalRSessionRealtimeAdapter> logger,
        string hubUrl)
    {
        _coordinator = coordinator;
        _authService = authService;
        _logger = logger;
        _hubUrl = hubUrl;
    }

    public async Task ConnectAsync()
    {
        if (_connection is not null)
        {
            _logger.LogWarning("SignalR connection already exists");
            return;
        }

        var token = await _authService.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Cannot connect to SignalR: no access token");
            return;
        }

        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options =>
            {
                options.AccessTokenProvider = async () => 
                    await _authService.GetAccessTokenAsync() ?? string.Empty;
            })
            .WithAutomaticReconnect()
            .Build();

        // Register event handlers
        _connection.On<SessionStartedEvent>("SessionStarted", OnSessionStarted);
        _connection.On<SessionEndedEvent>("SessionEnded", OnSessionEnded);
        _connection.On<SessionPausedEvent>("SessionPaused", OnSessionPaused);
        _connection.On<SessionResumedEvent>("SessionResumed", OnSessionResumed);

        try
        {
            await _connection.StartAsync();
            _logger.LogInformation("SignalR connection established");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to SignalR hub");
        }
    }

    public async Task DisconnectAsync()
    {
        if (_connection is null)
            return;

        try
        {
            await _connection.StopAsync();
            await _connection.DisposeAsync();
            _connection = null;
            _logger.LogInformation("SignalR connection closed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting from SignalR hub");
        }
    }

    private void OnSessionStarted(SessionStartedEvent evt)
    {
        _logger.LogInformation("Received SessionStarted event: {SessionId}, Source: {Source}", 
            evt.SessionId, evt.Source);
        _ = _coordinator.ApplyRemoteSessionStartedAsync(evt);
    }

    private void OnSessionEnded(SessionEndedEvent evt)
    {
        _logger.LogInformation("Received SessionEnded event: {SessionId}, Source: {Source}", 
            evt.SessionId, evt.Source);
        _ = _coordinator.ApplyRemoteSessionEndedAsync(evt);
    }

    private void OnSessionPaused(SessionPausedEvent evt)
    {
        _logger.LogInformation("Received SessionPaused event: {SessionId}, Source: {Source}", 
            evt.SessionId, evt.Source);
        _ = _coordinator.ApplyRemoteSessionPausedAsync(evt);
    }

    private void OnSessionResumed(SessionResumedEvent evt)
    {
        _logger.LogInformation("Received SessionResumed event: {SessionId}, Source: {Source}", 
            evt.SessionId, evt.Source);
        _ = _coordinator.ApplyRemoteSessionResumedAsync(evt);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await DisconnectAsync();
        _disposed = true;
    }
}
```

### 4. Implement SessionCoordinator Reconciliation Methods

**File**: `src/FocusBot.Infrastructure/Services/SessionCoordinator.cs`

Add the four reconciliation methods:

```csharp
public async Task ApplyRemoteSessionStartedAsync(SessionStartedEvent evt)
{
    await _lock.WaitAsync();
    try
    {
        // Ignore if event is from this device
        if (evt.Source == "desktop")
        {
            _logger.LogDebug("Ignoring SessionStarted from desktop source");
            return;
        }

        // If we already have this session, do nothing
        if (_currentState.HasActiveSession && _currentState.ActiveSession!.Id == evt.SessionId)
        {
            _logger.LogDebug("Session {SessionId} already exists locally", evt.SessionId);
            return;
        }

        // If we have a different active session, log conflict but don't auto-replace
        if (_currentState.HasActiveSession)
        {
            _logger.LogWarning("Remote session started while local session exists. Ignoring remote event.");
            return;
        }

        // Fetch full session details from API
        var session = await _apiClient.GetActiveSessionAsync();
        if (session is not null && session.Id == evt.SessionId)
        {
            _logger.LogInformation("Applied remote session start: {SessionId}", evt.SessionId);
            UpdateState(session, null, SessionChangeType.Synced);
        }
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
        // Ignore if event is from this device
        if (evt.Source == "desktop")
        {
            _logger.LogDebug("Ignoring SessionEnded from desktop source");
            return;
        }

        // Clear session if IDs match
        if (_currentState.HasActiveSession && _currentState.ActiveSession!.Id == evt.SessionId)
        {
            _logger.LogInformation("Applied remote session end: {SessionId}", evt.SessionId);
            UpdateState(null, null, SessionChangeType.Stopped);
        }
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
        // Ignore if event is from this device
        if (evt.Source == "desktop")
        {
            _logger.LogDebug("Ignoring SessionPaused from desktop source");
            return;
        }

        // Update session if IDs match
        if (_currentState.HasActiveSession && _currentState.ActiveSession!.Id == evt.SessionId)
        {
            var updated = await _apiClient.GetActiveSessionAsync();
            if (updated is not null && updated.Id == evt.SessionId)
            {
                _logger.LogInformation("Applied remote session pause: {SessionId}", evt.SessionId);
                UpdateState(updated, null, SessionChangeType.Paused);
            }
        }
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
        // Ignore if event is from this device
        if (evt.Source == "desktop")
        {
            _logger.LogDebug("Ignoring SessionResumed from desktop source");
            return;
        }

        // Update session if IDs match
        if (_currentState.HasActiveSession && _currentState.ActiveSession!.Id == evt.SessionId)
        {
            var updated = await _apiClient.GetActiveSessionAsync();
            if (updated is not null && updated.Id == evt.SessionId)
            {
                _logger.LogInformation("Applied remote session resume: {SessionId}", evt.SessionId);
                UpdateState(updated, null, SessionChangeType.Resumed);
            }
        }
    }
    finally
    {
        _lock.Release();
    }
}
```

**Key reconciliation rules**:
- **Source filtering**: Ignore events with `Source == "desktop"` to prevent echo (desktop → API → hub → desktop loop)
- **ID matching**: Only apply events that match the current session ID
- **Conflict avoidance**: If local session exists with different ID, log warning and ignore
- **Full fetch**: Always fetch complete session from API rather than reconstructing from lightweight event
- **Thread safety**: All methods use the same `_lock` as user-initiated commands

### 5. Wire Adapter Lifecycle to Authentication

**File**: `src/FocusBot.App/App.xaml.cs`

Update `OnAuthStateChangedAsync` to connect/disconnect SignalR:

```csharp
private async Task OnAuthStateChangedAsync()
{
    if (_services is null)
        return;

    var apiClient = _services.GetRequiredService<IFocusBotApiClient>();
    var adapter = _services.GetRequiredService<ISessionRealtimeAdapter>();

    if (_authService.IsAuthenticated)
    {
        var me = await apiClient.GetUserInfoAsync();
        if (me is null)
        {
            var logger = _services.GetRequiredService<ILogger<App>>();
            logger.LogWarning("Backend user provisioning failed; cloud features may be unavailable");
        }
        else
        {
            // Connect to SignalR hub after successful auth
            await adapter.ConnectAsync();
        }
    }
    else
    {
        // Disconnect SignalR and reset coordinator on sign-out
        await adapter.DisconnectAsync();
        var coordinator = _services.GetRequiredService<ISessionCoordinator>();
        coordinator.Reset();
    }
}
```

### 6. Update DI Registration

**File**: `src/FocusBot.App/App.xaml.cs`

Replace `NoOpSessionRealtimeAdapter` with `SignalRSessionRealtimeAdapter`:

```csharp
services.AddSingleton<ISessionRealtimeAdapter>(sp =>
{
    var hubUrl = GetFocusBotApiBaseUrl() + "/hubs/focus";
    return new SignalRSessionRealtimeAdapter(
        sp.GetRequiredService<ISessionCoordinator>(),
        sp.GetRequiredService<IAuthService>(),
        sp.GetRequiredService<ILogger<SignalRSessionRealtimeAdapter>>(),
        hubUrl
    );
});
```

### 7. Handle Reconnection Edge Cases

Add automatic session reconciliation on SignalR reconnect:

```csharp
_connection.Reconnected += async connectionId =>
{
    _logger.LogInformation("SignalR reconnected, reconciling session state");
    
    // Fetch current session from API and reconcile with local state
    var apiSession = await _apiClient.GetActiveSessionAsync();
    var localSession = _coordinator.CurrentState.ActiveSession;
    
    if (apiSession is not null && localSession is null)
    {
        // Remote session exists but not locally - sync it
        await _coordinator.ApplyRemoteSessionStartedAsync(new SessionStartedEvent(
            apiSession.Id,
            apiSession.SessionTitle,
            apiSession.SessionContext,
            apiSession.StartedAtUtc,
            apiSession.Source ?? "unknown"
        ));
    }
    else if (apiSession is null && localSession is not null)
    {
        // Local session exists but not remotely - it was ended elsewhere
        await _coordinator.ApplyRemoteSessionEndedAsync(new SessionEndedEvent(
            localSession.Id,
            DateTime.UtcNow,
            "unknown"
        ));
    }
    else if (apiSession is not null && localSession is not null && apiSession.Id != localSession.Id)
    {
        // Different sessions - remote wins (edge case: rapid start/stop across devices)
        _logger.LogWarning("Session ID mismatch after reconnect. Local: {LocalId}, Remote: {RemoteId}. Using remote.",
            localSession.Id, apiSession.Id);
        UpdateState(apiSession, null, SessionChangeType.Synced);
    }
};
```

### 8. Add Configuration for Hub URL

**Option A**: Use same base URL as API client (recommended)
```csharp
var hubUrl = GetFocusBotApiBaseUrl() + "/hubs/focus";
```

**Option B**: Add separate setting for SignalR hub (if different domain/port)
```csharp
private static string GetSignalRHubUrl()
{
#if DEBUG
    return "http://localhost:5251/hubs/focus";
#else
    return "https://api.foqus.me/hubs/focus";
#endif
}
```

---

## Testing Strategy

### Unit Tests

**File**: `tests/FocusBot.Infrastructure.Tests/Services/SignalRSessionRealtimeAdapterTests.cs`

Test cases:
- `ConnectAsync_ShouldBuildConnectionWithAccessToken`
- `DisconnectAsync_ShouldStopAndDisposeConnection`
- `OnSessionStarted_ShouldCallCoordinatorApplyRemoteSessionStarted`
- `OnSessionEnded_ShouldCallCoordinatorApplyRemoteSessionEnded`
- `OnSessionPaused_ShouldCallCoordinatorApplyRemoteSessionPaused`
- `OnSessionResumed_ShouldCallCoordinatorApplyRemoteSessionResumed`

**File**: `tests/FocusBot.Infrastructure.Tests/Services/SessionCoordinatorTests.cs`

Test reconciliation methods:
- `ApplyRemoteSessionStartedAsync_ShouldIgnoreDesktopSource`
- `ApplyRemoteSessionStartedAsync_ShouldFetchAndApplyWhenNoLocalSession`
- `ApplyRemoteSessionStartedAsync_ShouldIgnoreWhenDifferentLocalSessionExists`
- `ApplyRemoteSessionEndedAsync_ShouldClearMatchingSession`
- `ApplyRemoteSessionPausedAsync_ShouldUpdateMatchingSession`
- `ApplyRemoteSessionResumedAsync_ShouldUpdateMatchingSession`

### Integration/Manual Testing

Test cross-device scenarios:
1. **Start on web, see on desktop**: Start session on web dashboard → desktop shows active session
2. **Start on desktop, see on web**: Start session on desktop → web dashboard shows active session
3. **Pause on web, desktop updates**: Pause on web → desktop timer pauses
4. **Stop on desktop, web clears**: Stop on desktop → web dashboard shows "no session"
5. **Reconnection sync**: Disconnect network, start session on web, reconnect → desktop syncs session

---

## Rollout Checklist

- [ ] Create `SessionEvents.cs` with event DTOs in `FocusBot.Core/Entities`
- [ ] Add reconciliation methods to `ISessionCoordinator` interface
- [ ] Implement reconciliation methods in `SessionCoordinator`
- [ ] Create `SignalRSessionRealtimeAdapter` in `FocusBot.Infrastructure/Services`
- [ ] Update DI registration in `App.xaml.cs` to use SignalR adapter
- [ ] Wire adapter connect/disconnect to auth lifecycle in `OnAuthStateChangedAsync`
- [ ] Add reconnection reconciliation logic
- [ ] Write unit tests for adapter and reconciliation methods
- [ ] Manual test cross-device sync scenarios
- [ ] Update `docs/desktop-app.md` to mark phase 2 as complete
- [ ] Remove `NoOpSessionRealtimeAdapter.cs` after migration

---

## Edge Cases to Handle

### 1. Rapid Local Actions
**Scenario**: User clicks Stop button multiple times rapidly  
**Solution**: Coordinator semaphore already prevents concurrent mutations

### 2. Race: Local Start vs Remote Start
**Scenario**: User starts session on desktop while web start event arrives  
**Solution**: Local action completes first (holds lock), remote event sees matching ID and ignores

### 3. Offline Period
**Scenario**: Desktop offline for 10 minutes, session started and stopped on web  
**Solution**: On reconnect, reconciliation logic fetches API state and clears stale local session

### 4. Source Echo Prevention
**Scenario**: Desktop starts session → API emits event → desktop receives own event  
**Solution**: Filter out events with `Source == "desktop"`

### 5. Token Expiry During Connection
**Scenario**: SignalR connected, then access token expires  
**Solution**: Hub connection will fail auth and disconnect; `OnAuthStateChangedAsync` will trigger reconnect after token refresh

### 6. Multiple Desktop Instances (Same User)
**Scenario**: User runs two desktop app instances  
**Solution**: Both receive all events; source filtering ensures each instance only reacts to remote events

---

## Performance Considerations

- **Event frequency**: Low (typically < 10 events/hour per user)
- **Payload size**: Events are lightweight (< 500 bytes); full session fetch is ~1-2 KB
- **Connection overhead**: Persistent WebSocket; negligible CPU/memory impact
- **Reconnection**: Automatic with exponential backoff (SignalR default)

---

## Future Enhancements (Out of Scope)

- **Optimistic UI updates**: Show pause/resume immediately, reconcile on event confirmation
- **Conflict resolution UI**: Ask user which session to keep if both devices start simultaneously
- **Session ownership**: Track which device "owns" the session for smarter conflict resolution
- **Presence indicators**: Show which devices are currently connected on web dashboard

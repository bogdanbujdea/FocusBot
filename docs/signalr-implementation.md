# SignalR Implementation Guide

## Overview

SignalR enables real-time, bidirectional communication between the Foqus backend API and all connected clients (Windows desktop app, browser extension, web app). It synchronizes session lifecycle events across devices so that when a user starts a focus session on one device, all other logged-in devices are instantly notified.

**Key Purpose:** Cross-device session synchronization and real-time presence updates.

### Consistency Requirement (March 2026)

- Pause/resume must be **server-backed** on every platform.
- Local-only pause/resume state transitions are not allowed for normal authenticated flows.
- A pause/resume action is considered successful only after `POST /sessions/{id}/pause` or `POST /sessions/{id}/resume` succeeds.
- SignalR `SessionPaused` / `SessionResumed` is the cross-device propagation channel; clients must not re-post pause/resume when handling those remote events.

---

## Architecture

### Hub Location
- **Backend:** `src/FocusBot.WebAPI/Hubs/FocusHub.cs`
- **Endpoint:** `/hubs/focus`
- **Protocol:** WebSocket (with fallback to Server-Sent Events and Long Polling)

### Connected Clients

| Client | Platform | Package | Role | Connection |
|--------|----------|---------|------|-----------|
| **Web App** | React (Vite) | `@microsoft/signalr` | Read-only; receives real-time updates | Connects when user is on Dashboard/Analytics pages |
| **Windows Desktop App** | WinUI 3 (.NET 10) | `Microsoft.AspNetCore.SignalR.Client` | Produces & consumes; initiates most session events | Connects on login if cloud plan |
| **Browser Extension** | Chrome/Edge (Manifest V3) | `@microsoft/signalr` | Produces & consumes; background service worker | Connects from service worker on wake |

---

## Authentication

- **Method:** Supabase JWT bearer token
- **Token Location:** Query parameter `access_token` (required because WebSocket cannot send headers)
- **Validation:** Backend verifies JWT signature, expiry, and claims
- **User Group:** Connections automatically grouped by `userId` from JWT claims (`sub` claim)
- **Entitlement:** Free-plan users rejected with 403 on hub negotiate

---

## Hub Interface

### Server-to-Client Events (IFocusHubClient)

**Event: `SessionStarted`**
```csharp
Task SessionStarted(SessionStartedEvent e);

public sealed record SessionStartedEvent(
    Guid SessionId,
    string SessionTitle,
    string? SessionContext,
    DateTime StartedAtUtc,
    string Source  // "windows", "extension", "api", etc.
);
```
- **Trigger:** Any client calls `POST /sessions` to start a session
- **Broadcast:** To all other connections in the user's group
- **Latency Target:** < 500ms
- **Web App:** Calls `refreshActive()` to fetch and display the session
- **Desktop App:** Updates local session state and UI
- **Extension:** Receives notification (if connected)

---

**Event: `SessionEnded`**
```csharp
Task SessionEnded(SessionEndedEvent e);

public sealed record SessionEndedEvent(
    Guid SessionId,
    DateTime EndedAtUtc,
    string Source
);
```
- **Trigger:** Any client calls `POST /sessions/{id}/end` with summary metrics
- **Broadcast:** To all other connections in the user's group
- **Web App:** Clears active session display, refreshes analytics
- **Desktop App:** Stops tracking, may show completion summary
- **Extension:** Clears overlay state

---

**Event: `SessionPaused`**
```csharp
Task SessionPaused(SessionPausedEvent e);

public sealed record SessionPausedEvent(
    Guid SessionId,
    DateTime PausedAtUtc,
    string Source
);
```
- **Trigger:** Any client calls `POST /sessions/{id}/pause`
- **Broadcast:** To all other connections in the user's group
- **Effect:** Desktop app pauses local tracking; web app shows paused state

---

**Event: `SessionResumed`**
```csharp
Task SessionResumed(SessionResumedEvent e);

public sealed record SessionResumedEvent(
    Guid SessionId,
    string Source
);
```
- **Trigger:** Any client calls `POST /sessions/{id}/resume`
- **Broadcast:** To all other connections in the user's group
- **Effect:** Desktop app resumes tracking; web app shows active state

---

## Event Flow

### Scenario: User Starts Session on Windows App

```
1. Windows App → POST /sessions
   ├─ Creates session in database
   └─ Returns sessionId to desktop app

2. API → Broadcasts SessionStarted to user group
   ├─ Web App receives event
   │  └─ Calls refreshActive() → GET /sessions/active
   │     └─ Fetches new session data
   │        └─ Updates UI to show active session
   │
   ├─ Browser Extension receives event (if connected)
   │  ├─ Hydrates local activeSession from event payload (no extra API call)
   │  └─ Updates UI/badge immediately
   │
   └─ Other Desktop Instances receive event (if any)
      └─ Could trigger conflict resolution
```

### Scenario: User Ends Session from Web App

```
1. Web App → POST /sessions/{id}/end
   ├─ Ends session, stores summary metrics
   └─ Returns to API

2. API → Broadcasts SessionEnded to user group
   ├─ Windows App receives event
   │  └─ Stops local tracking
   │
   ├─ Browser Extension receives event
   │  └─ Clears overlay, resets state
   │
   └─ Web App receives event (itself)
      └─ Removes active session from UI
```

---

## Connection Lifecycle

### Desktop App (Windows)
```
Login (AuthState changes)
  ↓
FocusPageViewModel subscribes to hub events
  ↓
FocusHubClientService.ConnectAsync()
  ├─ Builds HubConnection with JWT token
  ├─ Sets up event handlers (SessionStarted, etc.)
  └─ Starts connection
     ├─ OnConnected: User group membership automatic
     ├─ OnReconnecting: Exponential backoff retry
     ├─ OnReconnected: Resume event handlers
     └─ OnClose: Log warning, attempt reconnect

Logout / Sign Out
  ↓
FocusPageViewModel unsubscribes
  ↓
FocusHubClientService.DisconnectAsync()
  ├─ Stops connection
  └─ Disposes resources
```

### Web App (React)
```
User Navigates to Dashboard Page
  ↓
useEffect hook in DashboardPage.tsx
  ├─ Checks if authenticated (session exists)
  └─ Calls connectFocusHub()
     ├─ Builds HubConnection with token factory
     ├─ Sets up callbacks for SessionStarted, etc.
     ├─ Starts connection
     └─ Automatic reconnect: 0s, 2s, 5s, 10s, 30s

User Navigates Away or Logs Out
  ↓
useEffect cleanup function fires
  ↓
Calls disconnectFocusHub()
  ├─ Stops connection
  └─ Sets connection to null
```

### Browser Extension (Background Service Worker)
```
Service Worker Startup
  ↓
connectFocusHub() called from `src/background/index.ts` (if auth session exists)
  ├─ Builds HubConnection with token
  ├─ Sets up event handlers
  └─ Starts connection

Service Worker Termination
  ↓
`focusbot-signalr-reconnect` alarm wakes worker
  ↓
On Alarm Wake
  ├─ Check if still logged in
  ├─ Reconnect to hub
  └─ Reconcile active session via GET /sessions/active (only after reconnect)

User Logs Out
  ↓
disconnectFocusHub() + clear reconnect alarm
```

Pause/resume mutation flow:

```
User or idle trigger in extension
  ↓
Resolve server session id (stored id first; fallback GET /sessions/active if needed)
  ↓
POST /sessions/{id}/pause or POST /sessions/{id}/resume
  ↓
API broadcasts SessionPaused / SessionResumed to user group
  ↓
All clients converge to server state
```

---

## Reconnection Strategy

**Automatic Reconnection:** Enabled on all clients
- **Backoff Delays:** 0ms, 2s, 5s, 10s, 30s (repeats up to max)
- **Transport:** WebSocket → Server-Sent Events → Long Polling
- **Timeout:** Default SignalR timeout (usually 30s)
- **Behavior:** Silently reconnects; logs warnings on failure

**Manual Reconnection:**
- Desktop App: `ConnectAsync()` called after auth token refresh
- Web App: Dashboard effect re-runs on session/route changes
- Extension: Triggered by `chrome.alarms` or manual wake; reconciles state with `GET /sessions/active` on startup/install/auth restore and on SignalR reconnected

---

## Conflict Resolution

### One Active Session Per User

**Rule:** A user can only have one active session across all devices.

**Scenario:** User has an active session on desktop, then tries to start a new session on their phone.

```
Phone → POST /sessions
  ↓
Backend detects conflict (409 Conflict)
  ├─ Returns existing active session details
  └─ Response includes: sessionId, deviceName, taskTitle

Phone Client UI
  ├─ Shows: "You have an active session on Desktop: 'Focus Work'"
  └─ Options:
     ├─ "End Remote Session" → POST /sessions/{id}/end → start new
     ├─ "Join Existing" → Use the same sessionId
     └─ "Cancel" → Don't start anything
```

---

## Message Format

### SessionStartedEvent (Example Payload)

```json
{
  "sessionId": "a44677aa-d125-4cb4-9fd8-f342943d744c",
  "sessionTitle": "Deep work on API design",
  "sessionContext": "Focusing on authentication flow",
  "startedAtUtc": "2026-03-27T10:15:30.000Z",
  "source": "windows"
}
```

### Constraints

- **Max Message Size:** < 1 KB (no full classification context in real-time pushes)
- **Encoding:** JSON
- **Timestamps:** Always UTC ISO 8601 format

---

## Client Implementation Details

### Web App (`src/foqus-web-app/src/api/signalr.ts`)

**Key Points:**
- Single global `connection` variable (one per browser tab)
- Early-return logic: Only skip if `state === HubConnectionState.Connected`
- Allows reconnection when state is `Disconnecting` or `Disconnected`
- Token factory refreshes auth token on each connection attempt
- Custom logger filters "stopped during negotiation" noise (React Strict Mode)

**Bug Fixed (March 2026):**
- Previously: `state !== HubConnectionState.Disconnected` blocked reconnection when disconnecting
- Fixed: `state === HubConnectionState.Connected` only skips when already connected
- Result: Web app now reliably receives events from desktop app sessions

**Usage:**
```typescript
import { connectFocusHub, disconnectFocusHub } from "../api/signalr";

void connectFocusHub({
  onSessionStarted: (event) => {
    void refreshActive();
    void loadTodayData();
  },
  onSessionEnded: (event) => {
    void refreshActive();
    void loadTodayData();
  },
  onSessionPaused: (event) => {
    void refreshActive();
  },
  onSessionResumed: (event) => {
    void refreshActive();
  },
});
```

### Desktop App (`src/FocusBot.Infrastructure/Services/FocusHubClientService.cs`)

**Key Points:**
- Implements `IFocusHubClient` interface
- Token provider uses `IAuthService.GetAccessTokenAsync()`
- Events wired to `FocusPageViewModel` for UI updates
- Automatic reconnection with exponential backoff
- User pause/resume commands are API-backed via `IFocusSessionOrchestrator.PauseSessionAsync()` / `ResumeSessionAsync()`
- Remote SignalR pause/resume applies local state only via `ApplyRemotePause()` / `ApplyRemoteResume()` (prevents API re-post loops)

### Browser Extension

**Key Points:**
- SignalR client: `browser-extension/src/shared/signalr.ts`
- Background integration: `browser-extension/src/background/index.ts`
- Connects from MV3 background service worker after sign-in and on startup/install when auth exists
- Uses `focusbot-signalr-reconnect` alarm (1-minute period) to reconnect after service worker suspension
- Handles `SessionStarted`, `SessionEnded`, `SessionPaused`, `SessionResumed` in background and broadcasts updated state
- Low-traffic hybrid sync: uses SignalR event payload as primary source and only calls `GET /sessions/active` on lifecycle boundaries (startup/install/auth restore/reconnect)
- On remote `SessionEnded`, ends local tracking and stores a completed session summary
- Local pause/resume actions (including idle-triggered pause/resume) now attempt server mutation first (`/pause`, `/resume`) and then update local state from server response
- If server session id is missing, extension reconciles via `GET /sessions/active` before pause/resume to avoid local/server id drift

---

## Logging and Monitoring

### Log Levels

| Level | Condition | Example |
|-------|-----------|---------|
| **Warn** | Connection failures, reconnecting, close | "failed to connect", "connection closed" |
| **Error** | Unrecoverable errors | "connection stop failed during teardown" |
| **Info** | (None by default; removed in cleanup) | Previously: "connection started", "reconnected" |

### Metrics to Monitor

- **Connection State Transitions:** `Connected` → `Disconnected` → `Connecting` → `Connected`
- **Event Latency:** Time from API broadcast to client receive
- **Reconnect Attempts:** Count per client per session
- **Event Delivery Success Rate:** Events successfully processed / events broadcast

---

## Error Handling

### Desktop App

```csharp
try {
    await hub.ConnectAsync();
}
catch (Exception ex) {
    logger.LogWarning(ex, "Focus hub connect failed");
    // App continues; automatic reconnect will attempt
}
```

### Web App

```typescript
try {
    await connection.start();
} catch (err) {
    const msg = err instanceof Error ? err.message : String(err);
    if (msg.includes("stopped during negotiation")) return; // Expected in Strict Mode
    console.warn(`[SignalR][focus] failed to connect`, {
        error: err,
        message: msg,
        state: connection?.state ?? "none",
        hasConnection: connection !== null,
    });
    // Automatic reconnect will attempt
}
```

---

## Performance Targets

| Metric | Target |
|--------|--------|
| Event latency (broadcast to all clients) | < 500ms |
| Max concurrent connections per user | 5 |
| Connection establish time | < 2s |
| Reconnection attempt interval | 0, 2, 5, 10, 30s |

---

## Known Limitations and Future Work

1. **No Redis Backplane:** Currently only supports single-server deployment. Requires Redis SignalR backplane for horizontal scaling.

2. **No Offline Queue in Web App:** Web app is read-only and doesn't need offline sync. Desktop app and extension have offline queue logic (planned).

3. **Service Worker Lifecycle (Extension):** Browser can terminate service worker at any time, breaking SignalR connection. Mitigated by `focusbot-signalr-reconnect` (`chrome.alarms`) periodic reconnect and accepting eventual consistency.

4. **Clock Drift:** No client-side clock sync. Events with timestamps > 5 minutes from server time are logged as warnings but still accepted.

5. **No Message Deduplication:** Clients handle duplicate events at the application layer (checked by sessionId + timestamp).

---

## Testing

### Manual Testing Checklist

- [ ] Start session on desktop → Verify web app updates immediately
- [ ] End session on web app → Verify desktop app stops tracking
- [ ] Pause session on desktop → Verify web app shows paused state
- [ ] Resume session on web app → Verify desktop app resumes
- [ ] Pause session in extension → Verify Windows app pauses within one SignalR round trip
- [ ] Resume session in extension → Verify Windows app resumes within one SignalR round trip
- [ ] Trigger extension idle pause → Verify desktop and web reflect paused state
- [ ] Trigger extension idle resume → Verify desktop and web reflect resumed state
- [ ] Kill desktop app while connected → Verify web app still updates
- [ ] Refresh web app during active session → Verify re-connects and loads session
- [ ] Logout from any client → Verify all clients disconnect from hub
- [ ] Start session on desktop while offline web → Bring web online → Verify session appears
- [ ] Try to start second session on phone while desktop has active → Verify 409 conflict UI

### Integration Tests

- `FocusBot.WebAPI.IntegrationTests`: Test hub broadcasts (mocked SignalR hub)
- Browser extension e2e: Test message routing between background and content scripts
- Web app e2e: Test connection lifecycle and event handlers

---

## References

- **Epic 6 (Cross-Device Sync):** `docs/MVP/epics/epic-6-cross-device-sync/epic6.md`
- **SignalR Docs:** https://learn.microsoft.com/en-us/aspnet/core/signalr/
- **@microsoft/signalr Docs:** https://github.com/dotnet/aspnetcore/tree/main/src/SignalR/clients/ts
- **FocusHub Source:** `src/FocusBot.WebAPI/Hubs/FocusHub.cs`
- **Web App SignalR Client:** `src/foqus-web-app/src/api/signalr.ts`
- **Desktop App SignalR Client:** `src/FocusBot.Infrastructure/Services/FocusHubClientService.cs`
- **Extension SignalR Client:** `browser-extension/src/shared/signalr.ts`
- **Extension Hub Wiring:** `browser-extension/src/background/index.ts`

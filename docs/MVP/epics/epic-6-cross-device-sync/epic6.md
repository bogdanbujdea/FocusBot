# Epic 6 — Cross-Device Sync

## Objective

Enable reliable data synchronization across all Foqus clients (Windows app, browser extension, web app) for cloud-plan users. Sessions, classification data, and task state are synced through the backend with conflict resolution, offline recovery, and real-time push via SignalR.

**Depends on:** Epic 1 (Contracts), Epic 2 (Windows App), Epic 3 (Web App), Epic 4 (Extension), Epic 5 (Full Analytics)

> This is the highest-complexity, highest-risk epic. It should only begin after all producers and the web dashboard are functional.

---

## Current State

| Component | Status | Details |
|---|---|---|
| Local WebSocket (desktop ↔ extension) | `[EXISTS]` | `ws://localhost:9876/focusbot` — single-machine shared task sync. Fully implemented. |
| Server-side session storage | `[EXISTS]` | `Session` entity in PostgreSQL. One-active-per-user constraint. |
| Cross-machine sync | `[NEW]` | No sync between devices on different machines. |
| Real-time server push | `[NEW]` | No SignalR hub. No server-initiated notifications. |
| Offline queue | `[NEW]` | No offline event queue in any client. |
| Conflict resolution | `[NEW]` | No merge logic for concurrent sessions from multiple devices. |

---

## Deliverables

1. SignalR hub implementation on the backend
2. SignalR client integration in Windows app, extension, and web app
3. Session sync pipeline (ingest + merge)
4. Classification event sync (if Option B from Epic 1 §7)
5. Offline queue and replay in native clients
6. Conflict/merge rules
7. Sync status UI in all clients
8. Entitlement enforcement (cloud plans only)

---

## Detailed Tasks

### 1. SignalR Hub — Backend `[NEW]`

#### Hub Setup

Add to `src/FocusBot.WebAPI/`:

```
Hubs/
├── FocusHub.cs
├── FocusHubMessages.cs
└── IFocusHubClient.cs (strongly-typed client interface)
```

**Registration:**
```csharp
builder.Services.AddSignalR();
app.MapHub<FocusHub>("/hubs/focus");
```

**Authentication:**
- Require Supabase JWT bearer token.
- Extract `userId` from JWT claims.
- Group connections by `userId` for broadcasting to all of a user's devices.

#### Hub Methods (Client → Server)

| Method | Payload | Behavior |
|---|---|---|
| `JoinUserGroup` | — | Adds the connection to the user's SignalR group (auto on connect) |
| `SubmitFocusStatus` | `{ sessionId, status, context, deviceId, timestamp }` | Broadcasts to all other devices in the user group |
| `NotifyTaskStarted` | `{ sessionId, taskTitle, startedAtUtc, deviceId }` | Broadcasts `TaskStarted` to other devices |
| `NotifyTaskEnded` | `{ sessionId, summary, deviceId }` | Broadcasts `TaskEnded` to other devices |

#### Hub Events (Server → Client)

| Event | Payload | Trigger |
|---|---|---|
| `TaskStarted` | `{ sessionId, taskTitle, startedAtUtc, deviceId, deviceName }` | Any device starts a task |
| `TaskEnded` | `{ sessionId, summary, deviceId }` | Any device ends a task |
| `FocusStatus` | `{ sessionId, status, context, deviceId, timestamp }` | Classification from any device |
| `DeviceOnline` | `{ deviceId, deviceType, name }` | Heartbeat received (device was offline) |
| `DeviceOffline` | `{ deviceId }` | Heartbeat timeout |
| `PlanChanged` | `{ planType, expiresAtUtc }` | Subscription webhook processed |
| `SyncCompleted` | `{ batchId, itemsSynced }` | Batch sync processed successfully |

#### Connection Lifecycle

- On connect: validate JWT, extract `userId`, add to user group, record connection for presence.
- On disconnect: remove from group, update presence (but don't mark device offline — that's heartbeat-driven).
- Reconnection: clients use exponential backoff (1s, 2s, 4s, 8s, max 30s).

### 2. Sync Model

#### What Gets Synced

| Data | Direction | When |
|---|---|---|
| Sessions | Client → Server | On session start (create), session end (summary), and periodic during session |
| Classification events | Client → Server | Near-real-time or batched (if Epic 1 §7 = Option B) |
| Task state (active task) | Bidirectional | On task start/end, via SignalR |
| Device identity | Client → Server | On registration and heartbeat |
| Plan/subscription status | Server → Client | On change, via SignalR |

#### What Does NOT Sync

- Local settings (provider, API key, overlay preferences) — these are device-specific.
- Classification cache — each client maintains its own cache.
- Extension-specific data (excluded domains, distraction overlay config).

#### Sync Granularity

| Mode | When | How |
|---|---|---|
| Real-time push (SignalR) | Task lifecycle, classification status, presence | SignalR messages |
| Near-real-time batch | Classification events during a session | HTTP POST batches every 30 seconds or on service worker wake |
| Synchronous | Session start/end | HTTP POST (wait for response) |
| Deferred | Offline recovery | HTTP POST when reconnected |

### 3. Ingestion Pipeline

#### Session Ingestion

Clients submit session data to the backend via existing endpoints:

- `POST /sessions` — start a session (returns server-side `sessionId`)
- `POST /sessions/{id}/end` — end a session with summary metrics

**Enhanced for sync:**
- Include `deviceId` in all requests.
- Backend validates that the device belongs to the authenticated user.
- Backend enforces one-active-session-per-user (existing constraint).

#### Event Ingestion (if Option B)

New endpoint: `POST /sessions/{id}/events`

```json
{
  "events": [
    {
      "eventId": "uuid",
      "timestamp": "2026-03-20T14:15:00Z",
      "eventType": "classification",
      "context": { "windowTitle": "...", "url": "...", "appName": "..." },
      "result": { "status": "NotAligned", "confidence": 0.92, "explanation": "..." }
    }
  ]
}
```

**Behavior:**
- Accept batches of up to 100 events per request.
- Use `eventId` for idempotency — skip duplicates silently.
- Validate that the session belongs to the user and is active or recently ended.
- Store in `SessionEvent` table (new entity).

#### Retry and Idempotency

- All sync operations use client-generated UUIDs as idempotency keys.
- Clients retry failed submissions with exponential backoff (1s, 2s, 4s, max 60s, max 5 retries).
- After max retries, queue locally and retry on next app/service-worker wake.

### 4. Offline Queue

#### Windows App

- When network is unavailable or backend returns 5xx:
  - Store pending session operations in SQLite (new `SyncQueue` table).
  - Columns: `Id`, `OperationType`, `Payload` (JSON), `CreatedAtUtc`, `RetryCount`, `LastAttemptUtc`.
- On reconnection:
  - Process queue in FIFO order.
  - Remove items after successful submission.
  - Increment `RetryCount` on failure.
  - Discard items after 7 days or 10 failed retries.

#### Browser Extension

- When network is unavailable:
  - Store pending operations in `chrome.storage.local` under a `syncQueue` key.
  - Format: array of `{ id, operationType, payload, createdAt, retryCount }`.
- On reconnection (service worker wake or `chrome.alarms` trigger):
  - Process queue in FIFO order.
  - Same retry/discard rules as Windows app.

#### Web App

- The web app is read-only for analytics — it does not produce sync data.
- No offline queue needed.

### 5. Conflict and Merge Rules

#### Active Session Conflicts

**Problem:** User starts a task on their home desktop, then starts a different task on their work desktop before ending the first.

**Rule:** One active session per user, enforced server-side.

**Resolution:**
1. Second device calls `POST /sessions` → backend returns `409 Conflict` with the existing active session.
2. Client receives 409 and shows: "You have an active session on [device name]: [task title]. End it first or join it."
3. Options:
   - **End remote session:** Call `POST /sessions/{id}/end` for the remote session, then start a new one.
   - **Join existing session:** Use the existing active session (classification from this device is attributed to the same session).
   - **Cancel:** Don't start a new session.

#### Classification Merge

- All devices are **independent producers** of classification events.
- Events are attributed to the session + device that produced them.
- No merging of classification results — each event stands on its own.
- Analytics aggregate across all events for a session (regardless of source device).

#### Timestamp Handling

- All timestamps are UTC.
- Clients send their local UTC time.
- Backend does **not** adjust timestamps (assumes clients have reasonable clock sync).
- If clock drift > 5 minutes is detected (event timestamp far from server receive time), log a warning but accept the data.

#### Data Precedence

- Session summary metrics (focus score, aligned/distracted time) are computed by the **ending device**.
- If a session spans multiple devices (e.g., started on desktop, joined on extension), the device that calls `POST /sessions/{id}/end` provides the final summary.
- Per-event analytics (if Option B) do not require merging — they are append-only.

### 6. Sync Status UI

#### Windows App

Add sync status indicator to the status bar or settings page:

| State | Icon | Description |
|---|---|---|
| Synced | ✓ | All local data submitted to cloud |
| Syncing | ↻ | Submission in progress |
| Offline | ⚠ | Network unavailable, queue building |
| Error | ✗ | Sync failed, will retry |
| Disabled | — | Free plan, no sync |

#### Browser Extension

Add sync status to the popup footer:

- "Synced" / "Syncing..." / "Offline (X items queued)" / "Sync error"
- Subtle, non-intrusive.

#### Web App — Integrations Page

Show per-device sync health:

| Column | Description |
|---|---|
| Status | Online / Offline |
| Last synced | Timestamp of last successful data submission |
| Queue size | Number of pending items (if offline and reportable via heartbeat) |

### 7. Entitlement Enforcement

| Action | Free | Cloud BYOK | Cloud Managed |
|---|---|---|---|
| Connect to SignalR | No | Yes | Yes |
| Submit sessions to cloud | No | Yes | Yes |
| Submit events to cloud | No | Yes | Yes |
| Receive cross-device task sync | No | Yes | Yes |
| View synced analytics | No | Yes (web) | Yes (web) |

**Enforcement:**
- **SignalR hub:** Reject connection if user is on Free plan (return 403 on negotiate).
- **Sync endpoints:** Return 403 if user is on Free plan.
- **Client-side:** Don't attempt sync operations if local plan type is Free.

### 8. SignalR Client Integration

#### Windows App

- Package: `Microsoft.AspNetCore.SignalR.Client`
- Create `ISignalRService` interface in Core, `SignalRService` implementation in Infrastructure.
- Connect on login (if cloud plan), disconnect on logout or plan downgrade.
- Handle:
  - `TaskStarted` / `TaskEnded` → update local session state, show notification
  - `FocusStatus` → update status bar if from another device
  - `PlanChanged` → refresh local plan state
  - `SyncCompleted` → update sync status UI
- **Coexistence:** SignalR runs alongside the local WebSocket server. Deduplicate by session ID + timestamp.

#### Browser Extension

- Package: `@microsoft/signalr`
- Connect from background service worker.
- Use `chrome.alarms` to reconnect if service worker was terminated.
- Handle same messages as Windows app.
- **Coexistence:** SignalR runs alongside the local WebSocket client. Deduplicate same as above.

#### Web App

- Package: `@microsoft/signalr`
- Connect when user is on the analytics or integrations page.
- Handle:
  - `DeviceOnline` / `DeviceOffline` → update integrations page in real time
  - `TaskStarted` / `TaskEnded` / `FocusStatus` → update dashboard if live view is active
  - `SyncCompleted` → refresh analytics data

---

## Risk Register

| Risk | Impact | Mitigation |
|---|---|---|
| Service worker termination breaks SignalR | Extension loses real-time sync | Use `chrome.alarms` for periodic reconnect; accept latency |
| Clock drift between devices | Events stored with wrong timestamps | Accept drift < 5 min; log warnings; don't reorder events |
| Offline queue grows too large | Storage pressure on clients | Cap queue size; discard items > 7 days; alert user |
| Multiple devices race to start sessions | 409 conflicts confuse users | Clear UX for conflict resolution (join/end/cancel) |
| Backend overload from event batches | API latency degrades | Rate-limit event ingestion; use batching; add request coalescing |
| SignalR connection limits | Too many concurrent connections per user | Cap at 5 simultaneous connections per user |

---

## Technical Notes

- SignalR uses WebSocket transport by default, with fallback to Server-Sent Events and Long Polling.
- Configure SignalR with Redis backplane if the WebAPI scales horizontally (not needed for MVP).
- All SignalR messages should be small (< 1 KB) — don't send full classification context in real-time pushes.
- Add structured logging for all sync operations (session ID, device ID, operation type, success/failure).
- Monitor sync queue depth as a health metric.

---

## Performance Targets

| Metric | Target |
|---|---|
| SignalR message latency (client → server → other clients) | < 500ms |
| Event batch ingestion (100 events) | < 1s |
| Offline queue replay (50 items) | < 10s |
| Sync status update after reconnect | < 5s |
| Max offline queue age before discard | 7 days |
| Max concurrent SignalR connections per user | 5 |

---

## Exit Criteria

- [ ] SignalR hub is implemented and authenticated
- [ ] Windows app connects to SignalR and receives cross-device task events
- [ ] Browser extension connects to SignalR (with service worker lifecycle handling)
- [ ] Web app receives real-time updates on integrations and analytics pages
- [ ] Sessions sync from all clients to the backend with device attribution
- [ ] Classification events sync (if Epic 1 §7 = Option B)
- [ ] Offline queue works in Windows app and extension — events are replayed on reconnect
- [ ] Session conflict (409) is handled with clear UX on all clients
- [ ] Sync status is visible in Windows app, extension, and web app
- [ ] Free users are blocked from sync features (client + server enforcement)
- [ ] Cloud users see unified cross-device analytics in the web dashboard
- [ ] Devices on different machines can share task state in real time
- [ ] Offline periods recover gracefully without data loss

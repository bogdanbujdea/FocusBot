# FocusBot: Desktop App and Browser Extension Integration

This document describes the integration between the FocusBot Windows desktop app and the FocusBot browser extension. The two communicate over a local WebSocket to keep a single shared task in sync and to exchange context (foreground app, browser URL) for focus classification.

---

## Overview

| Role | Component | Description |
|------|-----------|-------------|
| **Server** | Desktop app | Listens on `ws://localhost:9876/focusbot`. Accepts a single client (one extension instance). |
| **Client** | Browser extension | Connects to the app. Reconnects every 5 seconds if the app is not running. |

- **Single connection:** Only one extension connection is accepted at a time. A new connection closes the previous one.
- **Message format:** All messages use an envelope: `{ "type": "<MESSAGE_TYPE>", "payload": { ... } }`. JSON only.
- **No modes:** Both sides work independently. When connected, they share one active task (whoever started it) and show it as in progress on both sides. Either side can end it.

---

## Connection Lifecycle

### App side

1. On startup, the app starts the WebSocket server in `WebSocketIntegrationService.StartAsync()` (called from `App.OnLaunched`).
2. Server listens on `http://localhost:9876/focusbot/`.
3. When a client connects, the app accepts the WebSocket and raises `ExtensionConnectionChanged(true)`.
4. Connection state is surfaced to the UI via `KanbanBoardViewModel.IsExtensionConnected` (updated on the UI thread).
5. When the client disconnects or a new client connects, the previous socket is closed and `ExtensionConnectionChanged(false)` is raised.

### Extension side

1. The extension calls `startIntegration()` when the background script loads and on `chrome.runtime.onStartup` / `onInstalled`.
2. It connects to `ws://localhost:9876/focusbot`.
3. **On open:** It sends a **HANDSHAKE** with `hasActiveTask`, `taskId`, `taskText`, and optional `taskHints` (current session state).
4. If the connection fails or drops, it reconnects every **5 seconds** (fixed interval).
5. UI shows "Desktop App Connected" when `integration.connected` is true (WebSocket is open).

---

## Shared Task Behavior

- **One shared task:** Only one task is active across app and extension. Either the app or the extension can start it.
- **Display:** The side that did not start the task shows it as a normal in-progress task (same UI as a local task).
- **Conflict prevention:** Starting a new task is blocked if the other side already has an active task. The user sees a message to end the other task first.
- **Conflict on connect:** If both have an active task when the extension connects (e.g. app had a task and extension had a session while disconnected), **the app wins**: the extension clears its session and shows the app’s task.
- **Ending:** Either side can end the task; both sides clear it and return to idle.

---

## Message Types and Payloads

Message type strings are shared; payloads are JSON and must match between app and extension.

### HANDSHAKE

Exchanged when the extension connects. Used to sync whether either side has an active task.

| Direction | When | Payload |
|-----------|------|---------|
| Extension → App | Right after WebSocket opens (extension sends first). | `source`, `hasActiveTask`, `taskId?`, `taskText?`, `taskHints?` |
| App → Extension | After extension connects (app sends its current task state). | Same shape; `source: "app"`. |

**Behavior:**

- **Extension → App:** If `hasActiveTask` is true and `taskText` is non-empty, app shows that task as in progress (remote task from extension) and forwards desktop foreground to the extension.
- **App → Extension:** If app has an active task, it sends `hasActiveTask: true` and task details; extension shows the app’s task as the leader task. If app has no task, it sends `hasActiveTask: false`. If extension had a session and app has a task, extension clears its session (app wins).

### TASK_STARTED

Notifies the other side that a task has started. The sender’s task becomes the shared task.

| Field | Type | Description |
|-------|------|-------------|
| taskId | string | Unique task/session id. |
| taskText | string | Task description. |
| taskHints | string? | Optional context/hints. |

**Who sends:**

- **Extension → App:** When the user starts a session in the extension, only if the WebSocket is open. Otherwise the app learns via the next **HANDSHAKE** when the extension reconnects.
- **App → Extension:** When the user moves a task to In Progress in the app and the extension is connected.

**Effect:** The other side shows the task as in progress (same UI as a local task). If the receiver had its own task, it is cleared (sender wins).

### TASK_ENDED

Notifies that the current task has ended.

| Field | Type |
|-------|------|
| taskId | string |

**Effect:** Both sides clear the task and return to idle.

### FOCUS_STATUS

Sent by the side that is currently classifying focus (app when it has the task, extension when it has the session). The other side can show this in the UI.

| Field | Type | Description |
|-------|------|-------------|
| taskId | string | Task/session id. |
| classification | string | e.g. "aligned" / "distracting". |
| reason | string | Short explanation. |
| score | number | Numeric score. |
| focusScorePercent | number | Focus percentage. |
| contextType | string | e.g. "desktop" or "browser". |
| contextTitle | string | Window title or similar. |

### DESKTOP_FOREGROUND

Sent by the **app** to the **extension** whenever the foreground window changes and the extension is connected (always-on, no condition on who has the task).

| Field | Type |
|-------|------|
| processName | string |
| windowTitle | string |

**Effect:** If the extension has an active session, it classifies that window against the task and may send **FOCUS_STATUS** and update `currentDesktopContext`. Used for focus UI when the user is on a desktop app.

### BROWSER_CONTEXT

Sent by the **extension** to the **app** whenever the active browser tab URL or title changes (and when the extension connects). Pushed; no request/response.

| Field | Type |
|-------|------|
| url | string |
| title | string |

**Effect:** The app stores the latest payload in `LastBrowserContext`. When a browser window is in the foreground and the app has an active task, it uses this context (with desktop foreground) for classification instead of requesting the URL.

---

## Key Flows

### Extension starts a task

1. User starts a session in the extension (task text entered).
2. If connected: extension sends **TASK_STARTED** with `sessionId` and `taskText`; otherwise nothing until the next connection.
3. App receives **TASK_STARTED** → shows the task in the In Progress column (remote task). Window monitor starts so the app can send **DESKTOP_FOREGROUND**.
4. If the extension was not connected at step 2: when it later connects, it sends **HANDSHAKE** with `hasActiveTask: true` and task details → app shows the task as in progress (same as step 3).

### App starts a task

1. User moves a task to In Progress in the app.
2. If extension is connected and does not have a task: app sends **TASK_STARTED**. Extension shows the app’s task as the leader task.
3. If extension already had a session: app wins on next sync (extension clears session when it receives app’s state).

### Extension connects after app is already running

1. Extension opens WebSocket, then sends **HANDSHAKE** with its current session (if any).
2. App receives **HANDSHAKE**: if extension has `hasActiveTask: true` and non-empty `taskText`, app shows that task in the In Progress column.
3. App sends **HANDSHAKE** with its own state. Extension updates leader task. If both had tasks, extension clears its session (app wins).
4. Extension sends **BROWSER_CONTEXT** with the current tab so the app has it immediately.

### Context sharing (always-on)

- **App → Extension:** App sends **DESKTOP_FOREGROUND** on every foreground window change while the extension is connected.
- **Extension → App:** Extension sends **BROWSER_CONTEXT** on tab activation, tab URL/title update, and window focus change, and once after handshake when connected.

---

## Code Locations

| Concern | Desktop app | Extension |
|---------|-------------|-----------|
| WebSocket server / client | `FocusBot.Infrastructure/Services/WebSocketIntegrationService.cs` | `browser-extension/src/shared/integration.ts` |
| Message type constants | `FocusBot.Core/DTOs/IntegrationMessages.cs` (`IntegrationMessageTypes`) | `browser-extension/src/shared/integrationTypes.ts` (`MESSAGE_TYPES`) |
| Payload types | `FocusBot.Core/DTOs/IntegrationMessages.cs` | `browser-extension/src/shared/integrationTypes.ts` |
| Handling incoming messages | `WebSocketIntegrationService.HandleMessage` / `Handle*` methods | `browser-extension/src/background/index.ts` → `handleIntegrationMessage` |
| When app sends handshake / task started/ended | `KanbanBoardViewModel.OnExtensionConnectionChanged`, `NotifyTaskStartedAsync`, `NotifyTaskEndedAsync` | — |
| When extension sends handshake / task started/ended / browser context | — | `integration.ts` (`sendHandshake`, `sendTaskStarted`, `sendTaskEnded`, `sendBrowserContext`); `background/index.ts` (tab/focus listeners → `pushBrowserContextToApp`) |
| Remote task and connection in UI | `KanbanBoardViewModel` (`IsExtensionConnected`, `RemoteTaskFromExtension`, `DisplayInProgressTasks`, `IntegrationBlockedReason`) | `integration.ts` state (`leaderTaskId`, `leaderTaskText`); `SessionCard.tsx`; `AppShell.tsx` ("Desktop App Connected") |

---

## Configuration

- **Port:** 9876 (constant in both `WebSocketIntegrationService` and extension `integration.ts`).
- **Path:** `/focusbot` (extension uses `ws://localhost:9876/focusbot`; app listens on `http://localhost:9876/focusbot/`).
- **Reconnect:** Extension retries every **5 seconds** when the connection fails or drops.

No configuration file is required; both sides use these fixed values.

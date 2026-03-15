# FocusBot: Desktop App and Browser Extension Integration

This document describes the integration between the FocusBot Windows desktop app and the FocusBot browser extension. The two communicate over a local WebSocket so that task leadership, focus status, and context can stay in sync.

---

## Overview

| Role | Component | Description |
|------|-----------|-------------|
| **Server** | Desktop app | Listens on `ws://localhost:9876/focusbot`. Accepts a single client (one extension instance). |
| **Client** | Browser extension | Connects to the app, reconnects with backoff if the app is not running. |

- **Single connection:** Only one extension connection is accepted at a time. A new connection closes the previous one.
- **Message format:** All messages use an envelope: `{ "type": "<MESSAGE_TYPE>", "payload": { ... } }`. JSON only.

---

## Connection Lifecycle

### App side

1. On startup, the app starts the WebSocket server in `WebSocketIntegrationService.StartAsync()` (called from `App.OnLaunched`).
2. Server listens on `http://localhost:9876/focusbot/`.
3. When a client connects, the app accepts the WebSocket, sets `_clientSocket`, and raises `ExtensionConnectionChanged(true)`.
4. Connection state is surfaced to the UI via `KanbanBoardViewModel.IsExtensionConnected` (updated on the UI thread).
5. When the client disconnects or a new client connects, the previous socket is closed and `ExtensionConnectionChanged(false)` is raised.

### Extension side

1. The extension calls `startIntegration()` when the background script loads and on `chrome.runtime.onStartup` / `onInstalled`.
2. It connects to `ws://localhost:9876/focusbot`.
3. **On open:** It calls the handshake provider (current session state) and sends a **HANDSHAKE** with `hasActiveTask`, `taskId`, `taskText`, and optional `taskHints`.
4. If the connection fails or drops, it reconnects with exponential backoff (1s base, 30s max).
5. UI shows “Desktop App Connected” when `integration.connected` is true (WebSocket is open).

---

## Integration Modes

Both sides track a mode; they stay aligned via the messages below.

| Mode | Meaning |
|------|---------|
| **standalone** | No shared task. App shows Kanban; extension manages its own session independently. |
| **fullMode** | **App leads.** App has an in-progress task; extension follows (companion). Extension shows leader task and receives focus status and desktop foreground for classification. |
| **companionMode** | **Extension leads.** Extension has an active session; app shows the companion view and follows the extension’s task. |

Mode transitions are driven by **TASK_STARTED**, **TASK_ENDED**, and **HANDSHAKE** (see below).

---

## Message Types and Payloads

Message type strings are shared; payloads are JSON and must match between app and extension.

### HANDSHAKE

Exchanged when the extension connects, and sent by the app when the extension connection is established (to sync current state).

| Direction | When | Payload |
|-----------|------|---------|
| Extension → App | Right after WebSocket opens (extension sends first). | `source`, `hasActiveTask`, `taskId?`, `taskText?`, `taskHints?` |
| App → Extension | After extension connects (app sends its current task state). | Same shape; `source: "app"`. |

**Behavior:**

- **Extension → App:** If `hasActiveTask` is true and app is in `Standalone`, app switches to **CompanionMode** and, if `taskText` is non-empty, raises `TaskStartedReceived` so the UI shows the companion view.
- **App → Extension:** If app has an active task, it sends `hasActiveTask: true` and task details; extension sets mode to **companionMode** and shows the app’s task. If app has no task, it sends `hasActiveTask: false`; extension stays or goes to **standalone**.

### TASK_STARTED

Notifies the other side that a task has started (whoever is the leader).

| Field | Type | Description |
|-------|------|-------------|
| taskId | string | Unique task/session id. |
| taskText | string | Task description. |
| taskHints | string? | Optional context/hints. |

**Who sends:**

- **Extension → App:** When the user starts a session in the extension, **only if** the WebSocket is already open (`isConnected()`). Otherwise the app will learn via the next **HANDSHAKE** when the extension (re)connects.
- **App → Extension:** When the user starts a task in the app (task moves to In Progress) and the extension is connected.

**Effect:** Receiver switches to **companionMode** and shows the leader’s task (app shows companion view; extension shows leader task and may start classifying).

### TASK_ENDED

Notifies that the current task has ended.

| Field | Type |
|-------|------|
| taskId | string |

**Effect:** Both sides return to **standalone**.

### FOCUS_STATUS

Sent by the side that is currently evaluating focus (app when it’s leader, extension when it’s leader and classifying desktop/browser).

| Field | Type | Description |
|-------|------|-------------|
| taskId | string | Task/session id. |
| classification | string | e.g. "aligned" / "distracting". |
| reason | string | Short explanation. |
| score | number | Numeric score. |
| focusScorePercent | number | Focus percentage. |
| contextType | string | e.g. "desktop" or "browser". |
| contextTitle | string | Window title or similar. |

**Usage:** When the **app** is leader, it sends focus status to the extension so the extension UI can show current focus. When the **extension** is leader, it sends focus status after classifying the current page or after classifying the desktop (see DESKTOP_FOREGROUND).

### DESKTOP_FOREGROUND

Sent by the **app** to the **extension** when the user switches to a non-browser window and the app is in **FullMode** (app-led task). The extension then classifies that window against the task and may send back a **FOCUS_STATUS** and update its desktop context.

| Field | Type |
|-------|------|
| processName | string |
| windowTitle | string |

**Effect:** Extension (if it has an active session and is in fullMode) runs desktop classification and updates UI; it may send **FOCUS_STATUS** and set `currentDesktopContext`.

### REQUEST_BROWSER_URL

Sent by the **app** to the **extension** to ask for the current browser tab URL and title (e.g. for focus/alignment logic). The extension replies with **BROWSER_URL_RESPONSE**.

| Field | Type |
|-------|------|
| requestId | string |

### BROWSER_URL_RESPONSE

Sent by the **extension** in response to **REQUEST_BROWSER_URL**.

| Field | Type |
|-------|------|
| requestId | string |
| url | string |
| title | string |

---

## Key Flows

### Extension starts a task (extension leads)

1. User starts a session in the extension (task text entered).
2. If `isConnected()`: extension sends **TASK_STARTED** with `sessionId` and `taskText`; else nothing is sent until the next connection.
3. App receives **TASK_STARTED** → sets mode to **CompanionMode**, raises `TaskStartedReceived` → main window switches to companion view.
4. If the extension was not connected at step 2: when it later connects, it sends **HANDSHAKE** with `hasActiveTask: true` and task details → app does the same companion switch as in step 3.

### App starts a task (app leads)

1. User starts a task in the app (moves to In Progress).
2. App calls `NotifyTaskStartedAsync` → if extension is connected, sends **TASK_STARTED** and (on connection) had already sent **HANDSHAKE** with task state.
3. Extension receives **TASK_STARTED** (or **HANDSHAKE** with `hasActiveTask: true`) → sets mode to **companionMode**, shows app’s task.
4. App sends **DESKTOP_FOREGROUND** when the foreground window changes; extension classifies and may send **FOCUS_STATUS**. App may send **FOCUS_STATUS** for its own metrics; extension shows it when in companionMode.

### Extension connects after app is already running

1. Extension opens WebSocket, then sends **HANDSHAKE** with its current session (if any).
2. App receives **HANDSHAKE**: if extension has `hasActiveTask: true` and non-empty `taskText`, app goes to **CompanionMode** and shows companion view.
3. App then sends **HANDSHAKE** with its own state (has active task or not); extension updates its mode and leader task accordingly.

### Connection state in the UI

- **App:** `WebSocketIntegrationService` raises `ExtensionConnectionChanged` from the accept loop (background thread). `KanbanBoardViewModel.OnExtensionConnectionChanged` runs the property update and “send state on connect” logic on the **UI thread** so the “Extension connected” indicator and handshake/task sync work correctly.
- **Extension:** “Desktop App Connected” is shown when `integration.connected` is true (WebSocket open).

---

## Code Locations

| Concern | Desktop app | Extension |
|---------|-------------|-----------|
| WebSocket server / client | `FocusBot.Infrastructure/Services/WebSocketIntegrationService.cs` | `browser-extension/src/shared/integration.ts` |
| Message type constants | `FocusBot.Core/DTOs/IntegrationMessages.cs` (`IntegrationMessageTypes`) | `browser-extension/src/shared/integrationTypes.ts` (`MESSAGE_TYPES`) |
| Payload types | `FocusBot.Core/DTOs/IntegrationMessages.cs` | `browser-extension/src/shared/integrationTypes.ts` |
| Handling incoming messages | `WebSocketIntegrationService.HandleMessage` / `Handle*` methods | `browser-extension/src/background/index.ts` → `handleIntegrationMessage` |
| When app sends handshake / task started/ended | `KanbanBoardViewModel.OnExtensionConnectionChanged`, `NotifyTaskStartedAsync`, `NotifyTaskEndedAsync` | — |
| When extension sends handshake / task started/ended | — | `integration.ts` (`sendHandshake`, `sendTaskStarted`, `sendTaskEnded`); `background/index.ts` (`startSession` → `sendTaskStarted` when connected) |
| Mode and connection state in UI | `KanbanBoardViewModel` (`IsExtensionConnected`, `CompanionModeRequested`); `MainWindow` (companion view) | `integration.ts` state; `AppShell.tsx` (“Desktop App Connected”) |

---

## Configuration

- **Port:** 9876 (constant in both `WebSocketIntegrationService` and extension `integration.ts`).
- **Path:** `/focusbot` (extension uses `ws://localhost:9876/focusbot`; app listens on `http://localhost:9876/focusbot/`).
- **Reconnect:** Extension uses exponential backoff (1s base, 30s cap) when the connection fails or drops.

No configuration file is required; both sides use these fixed values.

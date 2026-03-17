# Foqus: Desktop App and Browser Extension Integration

This document describes the integration between the Foqus Windows desktop app and the Foqus browser extension. The two communicate over a local WebSocket to keep a single shared task in sync and to exchange context (foreground app, browser URL) for focus classification.

---

## Overview

| Role | Component | Description |
|------|-----------|-------------|
| **Server** | Desktop app | Listens on `http://localhost:9876/focusbot/`. Accepts a single WebSocket client (one extension instance). New connections close any existing one. |
| **Client** | Browser extension | Connects to `ws://localhost:9876/focusbot`. If the app is not running or the socket drops, reconnects every 5 seconds (fixed interval). |

- **Single connection:** Only one extension connection is accepted at a time. A new connection closes the previous one.
- **Message format:** All messages use an envelope: `{ "type": "<MESSAGE_TYPE>", "payload": { ... } }`. JSON only. Message type strings and payload shapes are shared between app and extension.
- **Single shared task:** There is exactly one active task across app and extension. Whoever started it (app or extension) owns it; the other side shows it as an in-progress task in the same UI as a local task (no separate “companion” or “remote” view). Either side can end the task; both clear it.

---

## Connection Lifecycle

### App side

1. On startup, the app starts the WebSocket server in `WebSocketIntegrationService.StartAsync()` (called from `App.OnLaunched`).
2. Server listens on `http://localhost:9876/focusbot/`.
3. When a client connects, the app accepts the WebSocket and raises `ExtensionConnectionChanged(true)`.
4. Connection state is surfaced to the UI via `KanbanBoardViewModel.IsExtensionConnected` (updated on the UI thread).
5. When the client disconnects or a new client connects, the previous socket is closed and `ExtensionConnectionChanged(false)` is raised.

### Extension side

1. The extension starts the WebSocket client when the background script loads (browser start or extension load), so it can connect as soon as the Windows app is running without the user opening the popup. Opening the popup or side panel still sends `START_DESKTOP_INTEGRATION` (calls `startIntegration()`) so connection is attempted then too if not already connected.
2. It connects to `ws://localhost:9876/focusbot`.
3. **On open:** It sends a **HANDSHAKE** with `hasActiveTask`, `taskId`, `taskText`, and optional `taskHints` (current session state).
4. If the connection fails or drops, the extension retries every **5 seconds** (fixed interval), so when the user starts the Windows app the extension connects within one interval.
5. UI shows "Desktop App Connected" when `integration.connected` is true (WebSocket is open).

---

## Shared Task Behavior

- **One shared task:** Only one task is active across app and extension. Either the app or the extension can start it.
- **Display on the app:** If the task was started in the extension, the app shows it as the **active task** (same card style as local tasks), built from `RemoteTaskFromExtension` and rendered via `DisplayInProgressTasks`. The status bar (app/window line and focus classification) appears and behaves the same as for an app-started task.
- **Display on the extension:** If the task was started in the app, the extension shows it as the “leader” task (e.g. in the session card) with the app’s task id and text; the extension does not create a separate session id for it.
- **Conflict prevention:** Starting a new task is blocked if the other side already has an active task. On the app, the user sees `IntegrationBlockedReason` (e.g. a message that the extension has an active task and they must end it first). On the extension, starting a session is blocked when `leaderTaskId` is set (app has the task).
- **Conflict on connect:** If both have an active task when the extension connects (e.g. app had a task and extension had a session while disconnected), the **app wins**: the extension clears its session and adopts the app’s task as the leader.
- **Ending:** Either side can end the task. The ending side sends **TASK_ENDED**; the other side clears its in-progress state and returns to idle. If the app ends the task, it also stops the window monitor and time tracking when there is no local in-progress task.

---

## Status Bar (Focus Status)

The status bar shows the **current app/window** and **focus status**. It behaves the same whether the task was started in the app or in the extension.

- **Visibility:** The whole block (window info line + colored bar) is visible when `IsMonitoring` is true. The app sets `IsMonitoring = DisplayInProgressTasks.Count > 0` in `RefreshDisplayInProgressTasks`, so the bar appears whenever there is at least one in-progress task (local or remote). The colored bar’s visibility is also tied to `IsMonitoring` (not to AI configuration), so extension-started tasks always show the bar.

- **Top line:** Displays process name, window title, and per-window elapsed time (e.g. "App: msedge | Window Title: Netflix - Personal | Time: 00:01:23"). Data comes from the desktop foreground window (updated in `OnForegroundWindowChanged`) or, when the app receives **FOCUS_STATUS** for the current remote task, from `contextType` and `contextTitle` in the payload. “App: \<process\> | Window Title: \<title\> | Time: \<elapsed\>”. Data comes from the desktop foreground window (updated in `OnForegroundWindowChanged`) or, when the app receives **FOCUS_STATUS** for the current remote task, from `contextType` and `contextTitle` in the payload.

- **Colored bar:**
  - **Icon:** Always visible when the bar is shown (bound to `IsMonitoring`). When there is no classification result yet, the icon is the neutral “unclear” icon; once the app or the extension has a result, the icon reflects the score (e.g. fire for focused, triangle exclamation for distracted), via `FocusStatusIcon`.
  - **Text:** Either “Checking…” when `ShowCheckingMessage` is true (`IsMonitoring && !HasCurrentFocusResult`), or the classification message: `FocusScoreCategory` (“Focused” / “Unclear” / “Distracted”) and `FocusReason`, when `HasCurrentFocusResult` is true.

- **Source of classification:**
  - **App-side:** When any task is in progress (local or remote), the app runs focus classification on foreground window changes. In `OnForegroundWindowChanged`, the “effective” task is either the first local in-progress task or, if there are no local in-progress tasks, the remote task from `RemoteTaskFromExtension` (using its `TaskId`, `TaskText`, `TaskHints`). The app then calls `ClassifyAndUpdateFocusAsync` or `ClassifyWithBrowserContextAsync` (using **BROWSER_CONTEXT** when a browser is in front). The result sets `FocusScore`, `FocusReason`, `HasCurrentFocusResult`, and notifies the status bar bindings.
  - **Extension:** If the extension sends **FOCUS_STATUS** for the current remote task, the app applies the payload in `OnIntegrationFocusStatusReceived` (score, reason, contextType, contextTitle, focusScorePercent) and updates the same status bar properties, so the bar can show either app or extension classification.

---

## Message Types and Payloads

Message type strings are shared; payloads are JSON and must match between app and extension.

### HANDSHAKE

Exchanged when the extension connects. Used to sync whether either side has an active task so both UIs show the same state.

| Direction | When | Payload |
|-----------|------|---------|
| Extension → App | Right after WebSocket opens (extension sends first). | `source`, `hasActiveTask`, `taskId?`, `taskText?`, `taskHints?` |
| App → Extension | After app processes the extension’s handshake (app sends its current task state). | Same shape; `source: "app"`. |

**Behavior:**

- **Extension → App:** The app handles the payload in the WebSocket layer and raises events. If `hasActiveTask` is true and `taskText` is non-empty, the app treats this as a remote task: sets `RemoteTaskFromExtension` (from payload), sets `_extensionHasActiveTask`, starts window monitor and time tracking, calls `RefreshDisplayInProgressTasks` so the task appears as the active task, and can forward desktop foreground to the extension. If the extension had no task, the app just records connection state.
- **App → Extension:** The app sends a handshake with its own state. If the app has an active task, it sends `hasActiveTask: true` and task details; the extension sets `leaderTaskId` / `leaderTaskText` and shows the app’s task (e.g. in the session card). If the app has no task, it sends `hasActiveTask: false`. If the extension had a session and the app has a task, the extension clears its session (app wins) and shows the app’s task.

### TASK_STARTED

Notifies the other side that a task has started. The sender’s task becomes the shared task.

| Field | Type | Description |
|-------|------|-------------|
| taskId | string | Unique task/session id. |
| taskText | string | Task description. |
| taskHints | string? | Optional context/hints. |
| startedAt | string? | Optional ISO 8601 UTC timestamp when the task started. The app uses this to show correct elapsed time for extension-started tasks. |

**Who sends:**

- **Extension → App:** When the user starts a session in the extension, only if the WebSocket is open. Otherwise the app learns via the next **HANDSHAKE** when the extension reconnects.
- **App → Extension:** When the user moves a task to In Progress in the app and the extension is connected.

**Effect:** The other side shows the task as in progress (same UI as a local task). If the receiver had its own task, it is cleared (sender wins). On the app, receiving **TASK_STARTED** from the extension sets `RemoteTaskFromExtension`, starts the window monitor and time tracking, and populates `DisplayInProgressTasks` with a synthetic in-progress task; elapsed time uses `startedAt` when provided.

### TASK_ENDED

Notifies that the current task has ended.

| Field | Type |
|-------|------|
| taskId | string |

**Effect:** The receiver clears its in-progress state (and, on the app, clears `RemoteTaskFromExtension` and stops window monitor and time tracking if there is no local in-progress task). Both sides return to idle.

### FOCUS_STATUS

Sent by the side that is currently classifying focus (app when it has the task, extension when it has the session). The other side shows this in the **status bar** (focus icon, classification message, reason, context line).

| Field | Type | Description |
|-------|------|-------------|
| taskId | string | Task/session id. |
| classification | string | e.g. "aligned" / "distracting". |
| reason | string | Short explanation. |
| score | number | Numeric score. |
| focusScorePercent | number | Focus percentage. |
| contextType | string | e.g. "desktop" or "browser". |
| contextTitle | string | Window title or similar. |

**App behavior when it receives FOCUS_STATUS:** If the payload matches the current remote task, the app updates the status bar: FocusScore, FocusReason, CurrentProcessName/CurrentWindowTitle (from contextType/contextTitle), focus icon, and classification text. So the bar reflects the extension’s classification. The app also runs its own classification when the foreground window changes (for extension-started tasks as well), so the bar can show either app or extension classification.

### DESKTOP_FOREGROUND

Sent by the **app** to the **extension** whenever the foreground window changes and the extension is connected. Sent unconditionally (no check on who has the task).

| Field | Type |
|-------|------|
| processName | string |
| windowTitle | string |

**When sent:** From `OnForegroundWindowChanged` in `KanbanBoardViewModel` (triggered by the window monitor). The app calls `SendDesktopForegroundAsync(processName, windowTitle)` when the extension is connected.

**Effect:** The extension receives the current desktop window. If the extension has an active session, it can classify that window against the task and send **FOCUS_STATUS** and update its `currentDesktopContext` for focus UI when the user is on a desktop app.

### BROWSER_CONTEXT

Sent by the **extension** to the **app** whenever the active browser tab URL or title changes, and once after the extension connects. Push-only; the app never requests the URL.

| Field | Type |
|-------|------|
| url | string |
| title | string |

**Effect:** The app stores the latest payload in `LastBrowserContext` (on `IIntegrationService`). When a browser window is in the foreground and the app has an active task (local or remote), `ClassifyWithBrowserContextAsync` uses this context together with the desktop foreground for classification (combined title/URL passed to the classifier). No request/response pattern: the extension pushes on tab activation, URL/title updates, and window focus changes.

---

## Key Flows

### Extension starts a task

1. User starts a session in the extension (task text entered).
2. If connected: extension sends **TASK_STARTED** with `sessionId` and `taskText`; otherwise nothing until the next connection.
3. App receives **TASK_STARTED** → shows the task in the In Progress column (remote task). Window monitor and time tracking start.
4. The **status bar** above the Kanban appears (same as for an app-started task). The app runs focus classification for the current foreground window (and uses **BROWSER_CONTEXT** when a browser is in front), so the user sees “Checking…” then the classification message and icon (Focused / Distracted, etc.) the same way as when the task was started in the app.
5. App sends **DESKTOP_FOREGROUND** on window changes; extension may send **FOCUS_STATUS** when it classifies. The app shows classification from either its own classification or incoming **FOCUS_STATUS**.
6. If the extension was not connected at step 2: when it later connects, it sends **HANDSHAKE** with `hasActiveTask: true` and task details → app shows the task as in progress (same as step 3).

### App starts a task

1. User moves a task to In Progress in the app.
2. If extension is connected and does not have a task: app sends **TASK_STARTED**. Extension shows the app’s task as the leader task.
3. If extension already had a session: app wins on next sync (extension clears session when it receives app’s state).

### Extension connects after app is already running

1. Extension opens WebSocket, then sends **HANDSHAKE** with its current session (if any): `hasActiveTask`, `taskId`, `taskText`, `taskHints`.
2. App receives **HANDSHAKE**: if extension has `hasActiveTask: true` and non-empty `taskText`, app sets `RemoteTaskFromExtension`, starts window monitor and time tracking, and shows that task as the active task via `DisplayInProgressTasks`; status bar appears and classification runs for the current window.
3. App sends **HANDSHAKE** with its own state. Extension updates leader task (`leaderTaskId`, `leaderTaskText`). If both had tasks, extension clears its session (app wins) and shows the app’s task.
4. Extension sends **BROWSER_CONTEXT** with the current tab so the app has `LastBrowserContext` immediately for classification when a browser window is in the foreground.

### Context sharing (always-on)

- **App → Extension:** The app sends **DESKTOP_FOREGROUND** on every foreground window change while the extension is connected. No check on who has the task; sending is unconditional.
- **Extension → App:** The extension sends **BROWSER_CONTEXT** on tab activation, tab URL/title update, and window focus change, and once after handshake when connected. The app always stores the latest value; no request from the app.

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
| Status bar for shared task | `KanbanBoardViewModel` (`IsMonitoring`, `DisplayInProgressTasks`, `FocusStatusIcon`, `FocusScore`, `FocusReason`, `ShowCheckingMessage`); `OnForegroundWindowChanged` (effective task = local or remote), `OnIntegrationFocusStatusReceived` | — |

---

## Cross-promotion and store links

Each side can prompt users to install the other component. Store URLs are defined in code and should be updated when the app or extension is published in each store.

### Extension-only users (app not connected)

When the extension UI is open and the desktop app is not connected, the extension shows a short message and a link to the Windows app:

- **Message (exact):** "Track the Windows apps you use and get focus alignment for them too. Install the Foqus Windows app from the Microsoft Store."
- **Link:** One clickable link to the Microsoft Store listing for the Foqus Windows app.
- **Where defined:** Extension: `browser-extension/src/shared/constants.ts` (`WINDOWS_STORE_APP_URL`, `INSTALL_APP_MESSAGE`). Rendered in `browser-extension/src/ui/AppShell.tsx`.

### App-only users (extension not installed or not connected)

The Windows app shows the extension message and store links in two places:

1. **How It Works dialog** — Always shown when the user opens the “?” help dialog. Section “Browser extension” with the message and two links: “Get it for Edge”, “Get it for Chrome”.
2. **Kanban header** — Shown only when the extension is **not** connected **and** the foreground window is **Microsoft Edge or Google Chrome** (not Firefox or other apps). Same message and the same two store links. When the extension is connected, the header shows “Extension Connected” instead; when the foreground app is not Edge/Chrome, the promo is hidden.

- **Message:** "Web page focus accuracy is improved when you use the Foqus browser extension."
- **Links:** Microsoft Edge Add-ons and Chrome Web Store (e.g. “Get it for Edge”, “Get it for Chrome”).
- **Where defined:** App: `FocusBot.Core/ExtensionStoreLinks.cs` (`EdgeAddOns`, `ChromeWebStore`). Used by `KanbanBoardViewModel` (`ExtensionStoreEdgeUri`, `ExtensionStoreChromeUri`) and by `HowItWorksDialog` code-behind for the dialog links.

### Publishing

When the Windows app or the browser extension is published in the Microsoft Store, Edge Add-ons, or Chrome Web Store, update the corresponding URL constant so the cross-promotion links point to the live listing.

---

## Browser extension storage (history)

The extension persists completed focus sessions for analytics and history. Raw visit data is not kept after a session ends.

- **CompletedSession format:** When a session ends, the extension saves a `CompletedSession` to `chrome.storage.local` under the key `focusbot.completedSessions`. Each entry includes: `sessionId`, `taskText`, optional `taskHints`, `startedAt`, `endedAt`, and a `summary` object (task name, total/aligned/distracting seconds, focus percentage, distraction count, context-switch cost, and pre-aggregated top distracting/aligned domains). The full `visits` array (per-tab visit history) is not stored.
- **Pruning:** Storage is capped at the most recent **100** completed sessions and sessions older than **90** days are removed. Pruning runs each time a session is ended.
- **Migration:** On startup or install, if `focusbot.completedSessions` does not exist, the extension migrates completed sessions from the legacy `focusbot.sessions` key (converting each to `CompletedSession` by stripping visits). Existing users keep their history without losing data.

---

## Configuration

- **Port:** 9876. Defined in `WebSocketIntegrationService` (app) and in `integration.ts` (extension). Not configurable via file.
- **Path:** `/focusbot`. Extension connects to `ws://localhost:9876/focusbot`; app listens on `http://localhost:9876/focusbot/`.
- **Reconnect:** Extension retries every 5 seconds whenever it is not connected (including after first failure), so starting the Windows app leads to connection within one interval. The popup/side panel still triggers `startIntegration()` when opened so connection is attempted immediately then too.
- **Single client:** The app accepts only one WebSocket client. A second connection (e.g. another browser profile or machine) closes the first.

No configuration file is required; both sides use these fixed values.

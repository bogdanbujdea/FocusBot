# Desktop App

The Foqus Windows desktop app is built with WinUI 3 and .NET 10. It monitors foreground windows, classifies activity alignment, and provides a floating overlay for quick focus control.

---

## Project Structure

| Project | Purpose |
|---|---|
| `FocusBot.App` | WinUI 3 UI, XAML views, DI wiring, Win32 overlay |
| `FocusBot.App.ViewModels` | Presentation logic (CommunityToolkit.Mvvm) |
| `FocusBot.Infrastructure` | Data access, Win32 services, auth, LLM providers |
| `FocusBot.Core` | Domain entities and interfaces |

---

## Pages

### FocusPage

Main board view showing the active task, focus score, session timeline, and recent activity.

### SettingsPage

User settings including account management, subscription info, API key configuration (BYOK), and overlay preferences.

#### Plan Selection Visibility

The Settings page shows plan selection options based on authentication and subscription state:

| User State | Plan Selection UI |
|---|---|
| **Not logged in** | Hidden — user must sign in first |
| **Trial (TrialFullAccess)** | Plan cards with features: BYOK ("You provide your own API key") and Premium ("Platform-managed AI — no API key needed") |
| **BYOK (CloudBYOK)** | Upgrade card for Foqus Premium |
| **Premium (CloudManaged)** | Hidden — user is on the highest tier |

Relevant ViewModel properties:
- `ShowPlanOptions` — true when signed in AND on trial
- `ShowUpgradeToCloudManaged` — true when signed in AND on BYOK plan
- `PlanEndDateLabel` — shows "Expires at: Apr 5, 11:59 AM" for trial, "Renews: Apr 5, 11:59 AM" for paid plans

All plan selection buttons open the billing page at `https://app.foqus.me/billing`.

---

## ViewModels

### PlanSelectionViewModel

Manages plan display and tier selection. Key properties:

| Property | Purpose |
|---|---|
| `CurrentPlan` | The user's current `PlanType` |
| `IsSignedIn` | Whether the user is authenticated |
| `PlanDisplayName` | Human-readable plan name |
| `PlanEndDateLabel` | Expiration or renewal date (e.g., "Expires at: Apr 5, 11:59 AM") |
| `ShowPlanOptions` | Show BYOK + Premium plan cards (trial users) |
| `ShowUpgradeToCloudManaged` | Show upgrade button (BYOK users) |
| `IsCloudBYOKPlan` | True when on BYOK plan (controls API key section visibility) |

### AccountSettingsViewModel

Handles magic-link authentication and sign-out.

### ApiKeySettingsViewModel

Manages BYOK API key storage (DPAPI encrypted) and provider/model selection.

### OverlaySettingsViewModel

Controls the floating focus overlay visibility preference. Raises `OverlayVisibilityChanged` when the setting changes, which `IOverlayService` subscribes to for show/hide.

### SessionPageViewModel

Main page ViewModel managing the session UI state. Consumes `ISessionCoordinator` state updates to display the active session panel or new session form.

| Property | Purpose |
|---|---|
| `NewSession` | Child ViewModel for the "Start session" form |
| `ActiveSession` | Child ViewModel for the in-progress session display (null when no session) |
| `HasActiveSession` | True when a session is active; controls which panel is visible |

**Session flow:**
1. `InitializeAsync()` calls `ISessionCoordinator.InitializeAsync()` which loads any existing active session from the API
2. When coordinator state changes to active session, `SessionPageViewModel` creates and displays `ActiveSession`
3. When the user starts a new session via `NewSession`, the coordinator orchestrates the API call
4. `SessionPageViewModel` subscribes to coordinator state changes and updates UI reactively

### NewSessionViewModel

Handles the "Start session" form with title and context input. Delegates start operations to `ISessionCoordinator`.

| Property | Purpose |
|---|---|
| `SessionTitle` | Required session title input |
| `SessionContext` | Optional context/notes input |
| `State` | Command state (Idle, Loading, Error) derived from coordinator state |

`StartCommand` calls `ISessionCoordinator.StartAsync()` which orchestrates the `POST /sessions` API call and state transitions.

### ActiveSessionViewModel

Displays the in-progress session with a live elapsed timer and pause/resume/stop controls. Uses `System.Timers.Timer` with `IUIThreadDispatcher` for UI updates. Subscribes to `ISessionCoordinator` state changes for reactive updates.

| Property | Purpose |
|---|---|
| `SessionTitle` | The session's title |
| `StartedAtUtc` | When the session started |
| `IsPaused` | Whether the session is paused |
| `ElapsedDisplay` | Live timer in `HH:MM:SS` format |
| `State` | Command state (Idle, Loading, Error) derived from coordinator state |
| `PauseResumeLabel` | "Pause" or "Resume" based on `IsPaused` |

**Commands:**
- `PauseOrResumeCommand` — calls `ISessionCoordinator.PauseAsync()` or `ResumeAsync()`
- `StopCommand` — calls `ISessionCoordinator.StopAsync()` which ends session with placeholder metrics
- `ClearErrorCommand` — calls `ISessionCoordinator.ClearError()`

Timer calculation matches the web app logic: `(now - startedAt) - totalPausedSeconds`, freezing on `pausedAtUtc` when paused.

**Stop behavior:** The coordinator sends zero metrics (`FocusScorePercent`, `FocusedSeconds`, etc.) until real analytics are tracked. The server persists these values and can be updated once the overlay tracks focus data.

---

## Services

### ISessionCoordinator

**Phase 1 (Current):** Central coordinator for session lifecycle state and API orchestration. Acts as the single source of truth for active session state. All ViewModels consume coordinator state reactively rather than managing API calls directly.

| Method | Purpose |
|---|---|
| `InitializeAsync()` | Load existing active session from API on startup |
| `StartAsync(title, context)` | Start new session; returns true on success |
| `PauseAsync()` | Pause active session |
| `ResumeAsync()` | Resume paused session |
| `StopAsync()` | End active session with placeholder metrics (zeros) |
| `ClearError()` | Clear error state |
| `Reset()` | Reset coordinator (called on sign-out) |

| Property/Event | Purpose |
|---|---|
| `CurrentState` | Immutable `SessionState` snapshot (phase, active session, error, change type) |
| `StateChanged` | Event raised on every state transition with full snapshot and `SessionChangeType` |

**Command serialization:** All mutations are serialized via `SemaphoreSlim` to prevent race conditions from rapid button clicks or concurrent API calls.

**Conflict handling:** On `409 Conflict` from stop/end operations, the coordinator reconciles state by treating the session as already ended (idempotent stop).

**Implementation detail:** The coordinator sends `EndSessionPayload(0, 0, 0, 0, 0, null, null)` when ending. Once the desktop tracks real focus metrics (via the overlay or background monitor), this payload can include actual values.

### ISessionRealtimeAdapter

**Current:** `SignalRSessionRealtimeAdapter` is registered as singleton and listens to `SessionStarted` from the FocusBot SignalR hub. Incoming events are reconciled through `ISessionCoordinator` so the desktop UI updates when a session starts on another client.

**Lifecycle wiring:** The adapter connects on authenticated state and disconnects/reset on sign-out in `App.xaml.cs`.

**Next:** Add `SessionEnded`, `SessionPaused`, and `SessionResumed` event reconciliation to complete full remote session lifecycle sync.

See [docs/platform-overview.md](platform-overview.md) for architecture details and [docs/integration.md](integration.md) for cross-device sync patterns.

### IOverlayService

Controls the floating focus overlay window (Win32 layered window). Initialized in `App.xaml.cs` after the UI dispatcher is available.

| Subscription | Purpose |
|---|---|
| `OverlaySettingsViewModel.OverlayVisibilityChanged` | Show/hide overlay based on user setting |
| `ISessionCoordinator.StateChanged` | Update overlay when session starts/pauses/stops |
| `IForegroundClassificationCoordinator.ClassificationChanged` | Update focus score and status color |

**Visual states:**
- **No session**: Empty purple circle
- **Active session**: Score percentage with color (green=focused, purple=neutral, red=distracted)
- **Error**: Orange color
- **Status change**: 3-second glow effect when focus status changes

### IForegroundClassificationCoordinator

Coordinates foreground window change detection with the classification API. Controlled directly by `ISessionCoordinator` when session state changes.

| Method | Purpose |
|---|---|
| `Start(title, context)` | Subscribe to `ForegroundWindowChanged` and begin classifying |
| `Stop()` | Unsubscribe from window changes and stop classifying |

**Session-driven behavior:**
- `ISessionCoordinator` calls `Start()` when a session starts or resumes
- `ISessionCoordinator` calls `Stop()` when a session pauses, ends, or is reset
- On each foreground window change, calls `IClassificationService.ClassifyAsync`
- Classification results are broadcast via SignalR `ClassificationChanged` to all connected clients

**Lifecycle:** No external wiring needed. The coordinator is a dependency of `ISessionCoordinator` which controls its lifecycle directly.

---

## Session Architecture

Focus sessions are **API-only** — there is no local SQLite storage for sessions. The desktop app uses `IFocusBotApiClient` to communicate with the FocusBot Web API for all session lifecycle operations:

| Operation | API Call |
|---|---|
| Start session | `POST /sessions` |
| Load active session | `GET /sessions/active` |
| Pause session | `POST /sessions/{id}/pause` |
| Resume session | `POST /sessions/{id}/resume` |
| End session | `POST /sessions/{id}/end` |

This enables cross-device session sync: a session started on the desktop is visible on the web dashboard, and vice versa.

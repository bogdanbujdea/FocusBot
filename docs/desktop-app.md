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

Controls the floating focus overlay visibility preference.

### SessionPageViewModel

Main page ViewModel managing the session UI state. On initialization, loads any existing active session from the API (`GET /sessions/active`) for cross-device sync.

| Property | Purpose |
|---|---|
| `NewSession` | Child ViewModel for the "Start session" form |
| `ActiveSession` | Child ViewModel for the in-progress session display (null when no session) |
| `HasActiveSession` | True when a session is active; controls which panel is visible |

**Session flow:**
1. `InitializeAsync()` is called after the page loads and the UI dispatcher is ready
2. If the API returns an active session, `ActiveSession` is populated and displayed
3. When the user starts a new session, `NewSession.SessionStarted` event fires
4. `SessionPageViewModel` subscribes and switches to the `ActiveSession` panel

### NewSessionViewModel

Handles the "Start session" form with title and context input.

| Property | Purpose |
|---|---|
| `SessionTitle` | Required session title input |
| `SessionContext` | Optional context/notes input |
| `IsBusy` | True while API call is in progress |
| `ErrorMessage` | Set when the API call fails |

`StartCommand` calls `POST /sessions` and raises `SessionStarted` on success.

### ActiveSessionViewModel

Displays the in-progress session with a live elapsed timer. Uses `System.Timers.Timer` with `IUIThreadDispatcher` for UI updates.

| Property | Purpose |
|---|---|
| `SessionTitle` | The session's title |
| `StartedAtUtc` | When the session started |
| `IsPaused` | Whether the session is paused |
| `ElapsedDisplay` | Live timer in `HH:MM:SS` format |

Timer calculation matches the web app logic: `(now - startedAt) - totalPausedSeconds`, freezing on `pausedAtUtc` when paused.

---

## Services

See [docs/platform-overview.md](platform-overview.md) for architecture details and [docs/integration.md](integration.md) for cross-device sync via SignalR.

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

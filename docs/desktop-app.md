# Foqus Desktop App (Windows)

Windows desktop application built with WinUI 3 / .NET 10. Monitors the foreground window, classifies alignment against the active task, calculates focus score, and displays a floating overlay.

---

## Architecture

Clean Architecture with strict dependency direction:

```
Core (no deps) → Infrastructure (Core only) → ViewModels (Core only) → App (all projects, DI wiring)
```

| Layer | Project | Target | Purpose |
|---|---|---|---|
| **Core** | `FocusBot.Core` | `net10.0` | Entities, interfaces, events, helpers. Zero project references. Only dependency: `CSharpFunctionalExtensions`. |
| **Infrastructure** | `FocusBot.Infrastructure` | `net10.0-windows` | Service implementations — Supabase auth, Win32 APIs, SignalR client, HTTP API client, SQLite cache, DPAPI encryption |
| **ViewModels** | `FocusBot.App.ViewModels` | `net10.0` | Presentation logic via CommunityToolkit.Mvvm source generators |
| **App** | `FocusBot.App` | `net10.0-windows` | WinUI 3 UI, XAML pages, Win32 overlay, DI wiring |

---

## Core Layer

### Interfaces (12)

| Interface | Purpose |
|---|---|
| `IAuthService` | Supabase magic-link auth: sign-in, token management, session restore. Events: `AuthStateChanged`, `ReAuthRequired` |
| `IClassificationService` | Classifies foreground window against active task. Cache-first (SQLite), then API fallback |
| `IClientService` | Client registration lifecycle: `RegisterAsync`, `DeregisterAsync`, `EnsureClientIdLoadedAsync`, `GetClientId` |
| `IExtensionPresenceService` | Tracks browser extension via local WebSocket. `IsExtensionOnline` property, `ConnectionStateChanged` event |
| `IFocusBotApiClient` | Typed HTTP client for WebAPI — sessions, classification, subscriptions, clients, account provisioning |
| `IFocusHubClient` | SignalR client — receives `SessionStarted/Ended/Paused/Resumed`, `PlanChanged`, `ClassificationChanged` |
| `IFocusSessionOrchestrator` | Central session logic: start/end/pause/resume, manual overrides, coordinates all services. `StateChanged` event |
| `ILocalSessionTracker` | In-memory time accounting: focused/distracted seconds, focus score, distraction count, session summary |
| `IPlanService` | Subscription plan cache: `GetCurrentPlanAsync`, `GetStatusAsync`, `GetTrialEndsAtAsync`. `PlanChanged` event |
| `ISettingsService` | Key-value settings: API key (DPAPI encrypted), provider/model selection, generic `Get/SetSettingAsync<T>` |
| `IUIThreadDispatcher` | Marshals work to WinUI 3 `DispatcherQueue` thread |
| `IWindowMonitorService` | Polls foreground window (Win32). Events: `ForegroundWindowChanged`, `Tick`, `UserBecameIdle`, `UserBecameActive` |

### Entities and Records

| Type | Kind | Purpose |
|---|---|---|
| `AlignmentResult` | class | Classification result: `Score` (0–10), `Reason` (string) |
| `AlignmentCacheEntry` | class | SQLite-cached classification keyed by `ContextHash` + `TaskContentHash` |
| `UserSession` | class | In-memory view of an active API-backed session (id, title, context, elapsed, scores) |
| `SessionSummary` | class | Aggregate stats for backend submission on session end |
| `SessionEndResult` | class | End-session outcome: summary + optional API error |
| `ApiResult<T>` | class | Generic API call result with HTTP status code and user-friendly error |

### Static Helpers and DTOs

| Type | Kind | Purpose |
|---|---|---|
| `DailyFocusSummary` | record | Daily analytics DTO: date, sessions, tracked/aligned/distracted seconds, focus score |
| `SessionDistractionSummary` | record | Per-session distraction breakdown DTO |
| `AppDistractionSummary` | record | Per-app distraction aggregate DTO |
| `ApiResultMappings` | static class | Extension methods mapping `ApiResult<T>` to `Result<T>` |
| `ExtensionStoreLinks` | static class | Chrome/Edge store URLs for the browser extension |
| `ProviderInfo` | record | LLM provider metadata: `Id`, `DisplayName`, `ApiKeyUrl` |
| `ModelInfo` | record | LLM model metadata: `Id`, `DisplayName` |

### SignalR Event Records (`IFocusHubClient`)

| Record | Fields |
|---|---|
| `SessionStartedEvent` | `SessionId`, `SessionTitle`, `SessionContext?`, `StartedAtUtc`, `Source` |
| `SessionEndedEvent` | `SessionId`, `EndedAtUtc`, `Source` |
| `SessionPausedEvent` | `SessionId`, `PausedAtUtc`, `Source` |
| `SessionResumedEvent` | `SessionId`, `Source` |
| `PlanChangedEvent` | *(empty)* |
| `ClassificationChangedEvent` | `Score`, `Reason`, `Source`, `ActivityName`, `ClassifiedAtUtc`, `Cached` |

### API Model Records

| Record | Purpose |
|---|---|
| `StartSessionPayload` | `POST /sessions` request body |
| `EndSessionPayload` | `POST /sessions/{id}/end` request body |
| `ClassifyPayload` | `POST /classify` request body |
| `ValidateKeyPayload` | `POST /classify/validate-key` request body |
| `RegisterClientRequest` | `POST /clients` request body |
| `ApiSessionResponse` | Session API response |
| `ApiClassifyResponse` | Classification API response |
| `ApiSubscriptionStatus` | `GET /subscriptions/status` response |
| `ApiClientResponse` | Client registration API response |
| `ApiValidateKeyResponse` | Key validation API response |

### Enums

| Enum | Values |
|---|---|
| `ApiKeyMode` | `Own`, `Managed`, `Trial` |
| `ClientType` | `Desktop = 1`, `Extension = 2` |
| `ClientHost` | `Unknown = 0`, `Windows = 1`, `Chrome = 2`, `Edge = 3` |
| `FocusStatus` | `Distracted`, `Neutral`, `Focused` |
| `ClientPlanType` | `FreeBYOK = 0`, `CloudBYOK = 1`, `CloudManaged = 2` |
| `ClientSubscriptionStatus` | `None = 0`, `Trial = 1`, `Active = 2`, `Expired = 3`, `Canceled = 4` |

### Events

| Type | Fields | Purpose |
|---|---|---|
| `ForegroundWindowChangedEventArgs` | `ProcessName`, `WindowTitle` | From window monitor |
| `FocusSessionStateChangedEventArgs` | Full session state snapshot (elapsed, score, classification, errors, process/window) | Orchestrator → UI |
| `FocusOverlayStateChangedEventArgs` | `HasActiveSession`, status, score, paused, loading, error, tooltip | Orchestrator → Overlay |

### Helpers

| Type | Methods |
|---|---|
| `FocusScoreHelper` | `ComputeFocusScorePercentage(focused, distracted)` — `focused / (focused + distracted) * 100` |
| `HashHelper` | SHA-256 hashing, window title normalization, context hash computation |
| `TimeFormatHelper` | `FormatElapsed` (HH:mm:ss), `FormatTimeShort` (compact) |

### Configuration

| Type | Purpose |
|---|---|
| `FocusSessionConfig` | `PersistIntervalSeconds = 5` |
| `LlmProviderConfig` | Static provider/model catalog — OpenAI (`gpt-4o-mini`, `gpt-4.1-mini`, `gpt-5-nano-2025-08-07`), Anthropic (`claude-opus-4-6`, `claude-sonnet-4-6`, `claude-haiku-4-5`), Google (`gemini-embedding-001`, `gemini-2.5-flash-lite`, `gemini-2.5-flash`) |
| `SettingsKeys` | `HasSeenHowItWorksGuide`, `TrialWelcomeSeen` |

---

## Infrastructure Layer

### Services (11)

| Service | Implements | External Dependencies |
|---|---|---|
| `SupabaseAuthService` | `IAuthService` | Supabase SDK + HttpClient |
| `AlignmentClassificationService` | `IClassificationService` | `IFocusBotApiClient` + `AppDbContext` (SQLite cache) |
| `DesktopClientService` | `IClientService` | `IFocusBotApiClient`, `ISettingsService` |
| `ExtensionPresenceService` | `IExtensionPresenceService` | WebSocket server (`localhost:9876`) |
| `FocusBotApiClient` | `IFocusBotApiClient` | HttpClient → WebAPI, Polly retries, `IAuthService` for JWT |
| `FocusHubClientService` | `IFocusHubClient` | SignalR Client → `/hubs/focus` |
| `FocusSessionOrchestrator` | `IFocusSessionOrchestrator` | Coordinates: `ILocalSessionTracker`, `IWindowMonitorService`, `IClassificationService`, `IFocusBotApiClient`, `IClientService`, `IExtensionPresenceService` |
| `LocalSessionTracker` | `ILocalSessionTracker` | Pure in-memory — no external deps |
| `PlanService` | `IPlanService` | `IFocusBotApiClient` |
| `SettingsService` | `ISettingsService` | DPAPI (DataProtection) for key encryption, file-system settings |
| `WindowMonitorService` | `IWindowMonitorService` | Win32 `GetForegroundWindow`, `GetLastInputInfo` P/Invoke |

### Local SQLite Database

`AppDbContext` manages a single table — **alignment cache only**. Sessions are API-managed, not stored locally.

| Table | Entity | Key | Purpose |
|---|---|---|---|
| `AlignmentCacheEntries` | `AlignmentCacheEntry` | `(ContextHash, TaskContentHash)` composite | Cache classification results locally to avoid repeat API calls |

### API Client Resilience

`FocusBotApiClient` wraps session start/end calls with **Polly** retries:
- 3 attempts, 2-second delay between attempts
- Retries on transient HTTP failures (5xx, 408, `HttpRequestException`)

---

## ViewModels Layer

| ViewModel | Lines | Key Responsibilities |
|---|---|---|
| **`FocusPageViewModel`** | ~858 | Main focus board: start/end/pause/resume sessions, session timer, focus score bar, overlay state, SignalR hub integration, "How it works" + trial dialogs |
| **`FocusStatusViewModel`** | ~116 | Foreground window status bar: process name, window title, live focus score, classification reason |
| **`SettingsViewModel`** | ~32 | Composes settings sub-ViewModels, back navigation |
| **`ApiKeySettingsViewModel`** | ~310 | API key CRUD, provider/model selection, key validation, masked display |
| **`AccountSettingsViewModel`** | ~124 | Magic-link sign-in/sign-out, email display, auth state tracking |
| **`OverlaySettingsViewModel`** | ~45 | Overlay toggle (enabled/disabled), persists setting |
| **`PlanSelectionViewModel`** | ~271 | Plan display, trial/active/expired status, upgrade CTA, billing portal link |

All ViewModels use **CommunityToolkit.Mvvm** source generators: `[ObservableProperty]`, `[RelayCommand]`, `[NotifyPropertyChangedFor]`.

---

## App Layer (UI)

### Pages and Views

| View | Type | Purpose |
|---|---|---|
| `FocusPage.xaml` | XAML Page | Main focus session UI (bound to `FocusPageViewModel`) |
| `SettingsPage.xaml` | XAML Page | Settings tabs — account, API key, overlay, plan |
| `FocusOverlayWindow.cs` | Pure Win32 | 96px floating overlay (see below) |
| `HowItWorksDialog.xaml` | ContentDialog | First-run onboarding guide |
| `TrialWelcomeDialog.xaml` | ContentDialog | One-time trial welcome (shown for trial users) |
| `BYOKKeyPromptDialog.xaml` | ContentDialog | Prompts Foqus BYOK users to configure API key |
| `FocusStatusControl.xaml` | UserControl | Reusable focus status display |

### DI Registration

All wiring in `App.xaml.cs`:

| Registration | Lifetime |
|---|---|
| `AppDbContext` (SQLite) | Scoped |
| `ISettingsService` → `SettingsService` | Singleton |
| `IAuthService` → `SupabaseAuthService` | Singleton |
| `IWindowMonitorService` → `WindowMonitorService` | Singleton |
| `IUIThreadDispatcher` → `AppUIThreadDispatcher` | Singleton |
| `IFocusBotApiClient` → `FocusBotApiClient` | Singleton |
| `IClassificationService` → `AlignmentClassificationService` | Scoped |
| `ILocalSessionTracker` → `LocalSessionTracker` | Singleton |
| `IClientService` → `DesktopClientService` | Singleton |
| `IPlanService` → `PlanService` | Singleton |
| `INavigationService` → `MainWindowNavigationService` | Singleton |
| `IExtensionPresenceService` → `ExtensionPresenceService` | Singleton |
| `IFocusSessionOrchestrator` → `FocusSessionOrchestrator` | Singleton |
| `IFocusHubClient` → `FocusHubClientService` | Singleton |
| All ViewModels | Transient (except `OverlaySettingsViewModel` = Singleton) |

### Startup Flow

1. Restore auth (`TryRestoreSessionAsync`) + handle protocol activation (magic-link deep link)
2. Subscribe to `AuthStateChanged`
3. Start extension WebSocket presence server (`ExtensionPresenceService`)
4. Create `FocusPageViewModel` + `MainWindow`
5. Set `DispatcherQueue` on `AppUIThreadDispatcher`
6. Connect SignalR hub (if authenticated)
7. Activate window + create `FocusOverlayWindow`
8. Subscribe to overlay settings and focus state changes

---

## Focus Session Lifecycle

Sessions are **API-only** — the desktop app uses the WebAPI as the only source of truth. There is no offline session mode.

### Starting a Session

1. User enters task text on FocusPage
2. ViewModel calls `POST /sessions` via `IFocusBotApiClient` (with Polly retry)
3. API creates session, returns `ApiSessionResponse`
4. `FocusSessionOrchestrator` begins monitoring: subscribes to `IWindowMonitorService` events
5. `LocalSessionTracker` starts accumulating focused/distracted time
6. SignalR broadcasts `SessionStarted` to all user devices

### During a Session

- `IWindowMonitorService` polls foreground window every tick
- On window change: `IClassificationService` classifies (cache → API fallback)
- `ILocalSessionTracker` accumulates time per classification result
- `FocusSessionStateChangedEventArgs` broadcast to UI on every change
- `FocusOverlayStateChangedEventArgs` broadcast to overlay

### Pause / Resume

- **User pause**: `POST /sessions/{id}/pause` → SignalR `SessionPaused`
- **Idle pause**: After 5 minutes of no input (`GetLastInputInfo`), auto-pauses
- **Resume**: `POST /sessions/{id}/resume` → SignalR `SessionResumed`
- Only idle-paused sessions auto-resume when user becomes active; manually paused sessions stay paused
- `totalPausedSeconds` accumulates all pause intervals

### Ending a Session

1. `FocusSessionOrchestrator` finalizes `LocalSessionTracker` → `SessionSummary`
2. Calls `POST /sessions/{id}/end` with summary metrics (focus score, aligned/distracted seconds, distraction count)
3. Stops window monitoring
4. SignalR broadcasts `SessionEnded`

### Cross-Device Sync

After sign-in, the desktop app connects to the SignalR hub. `FocusPageViewModel` handles:
- `SessionStarted` / `SessionEnded`: reloads active session from API
- `SessionPaused` / `SessionResumed`: mirrors if matching current session
- `PlanChanged`: refreshes `IPlanService` cache

Hub connection is established **after** `FocusPageViewModel` creation to ensure event handlers are subscribed before the first connect.

---

## Focus Overlay

`FocusOverlayWindow.cs` — a **pure Win32 layered window** (no XAML). Always-on-top floating circle showing focus status.

### Specifications

| Property | Value |
|---|---|
| Size | 96px circle + 8px glow padding = 112px total |
| Rendering | `UpdateLayeredWindow` with per-pixel alpha via GDI+ |
| Position | Topmost, draggable, persists position |
| Click | Activates main window |
| Glow | 3-second glow animation on status change |

### Color States

| State | Color | Condition |
|---|---|---|
| Focused (aligned) | Green | Score > 5 |
| Neutral | Purple | Score = 5 |
| Distracted | Red | Score < 5 |
| Loading | Gray | Classification in progress |
| Error | Orange | Classification error |
| Paused | Gray (dimmed) | Session paused |
| No session | Neutral circle | No active session |

When a session is active, the overlay shows the focus score percentage (0–100).

---

## Classification Display States

The FocusPage shows classification status with these states:

| State | Display | Condition |
|---|---|---|
| Paused | "Paused" | Session is paused |
| Analyzing | "Analyzing..." | Classification in progress |
| Classifier error | Error message | Classification failed |
| Aligned | Green indicator + reason | Score > 5 |
| Neutral | Purple indicator + reason | Score = 5 |
| Distracted | Red indicator + reason | Score < 5 |
| Waiting for signal | Gray | No classification yet |

### Extension Connection Indicator

When the browser extension is connected via local WebSocket, FocusPage shows a connection indicator. When connected, desktop skips classifying browser windows (extension handles those).

---

## Trial UX

### Trial Welcome Dialog

After the first-run "How it works" dialog, signed-in users on the 24h trial (`PlanType.TrialFullAccess` + `status = Trial`) see a one-time `TrialWelcomeDialog` unless `SettingsKeys.TrialWelcomeSeen` is set. **View plans** opens `https://app.foqus.me/billing`. Also shown when user signs in after launch.

### Trial Banner

`InfoBar` on FocusPage with countdown and **Manage plan** button (billing URL):
- Shown when `ClientPlanType.FreeBYOK` (maps to server `TrialFullAccess`), `status = Trial`, and `trialEndsAt` is in the future
- **Subscription required** banner when trial has ended (same tier, expired or past `trialEndsAt`) with **View plans** button

### Foqus BYOK Key Prompt

After `PlanChanged` / hub refresh, if plan is `CloudBYOK` and no API key in settings:
- `BYOKKeyPromptDialog` prompts once per session to open Settings
- **Later** dismisses for that session
- DPAPI encryption info shown in dialog

---

## WinUI 3 Design System

### Elevation Layers

3 distinct surface layers (consistent across Light/Dark/High Contrast):

| Layer | Purpose | Dark | Light |
|---|---|---|---|
| Page (darkest) | Background | `#110E1A` | `#EDE9F5` |
| Section (medium) | Column/panel | `#1C1730` | `#F5F3FA` |
| Card (lightest) | Content card | `#2A2242` | `#FFFFFF` |

### Materials

- **Light/Dark**: Semi-transparent blurred surfaces (AcrylicBrush)
- **High Contrast**: Solid backgrounds only

### Theme File Layout

```
Themes/Colors.xaml    → ThemeDictionaries with Fb* Color resources
Themes/Brushes.xaml   → Semantic brushes (AcrylicBrush in Light/Dark, SolidColorBrush in HighContrast)
Themes/Styles.xaml    → Shared styles consuming semantic brushes
App.xaml              → Merge in order: Colors → Brushes → Styles
```

All colors use `Fb*` prefixed resources — never hardcoded hex in views.

### Design Tokens

| Token | Value |
|---|---|
| Corner radius (small) | 6px |
| Corner radius (medium) | 10px |
| Corner radius (large) | 12px |
| Page margin | 32px |
| Section gap | 16px |
| Card gap | 10px |
| Card padding | 12–14px |
| Body font | Segoe UI Variable |
| Section header | 12px SemiBold uppercase |
| Task title | 14px Medium |

### Accessibility

- Never encode meaning with color alone — pair with icon, text label, or accent bar
- Preserve focus indicators and keyboard navigation
- Use `AutomationProperties.Name` on interactive elements
- High Contrast: solid backgrounds, 3 distinct layers, readable text

---

## Store Submission

- **Display name**: "Foqus" (changed from "FocusBot.App")
- **Device family**: Desktop only (`Windows.Desktop`)
- **Package**: MSIX via Partner Center
- **Store listing**: See Partner Center for current listing text and screenshots

---

## Building and Testing

```bash
# Build
dotnet build src/FocusBot.App/FocusBot.App.csproj

# Tests (Windows-only)
dotnet test tests/FocusBot.App.ViewModels.Tests/FocusBot.App.ViewModels.Tests.csproj
dotnet test tests/FocusBot.Infrastructure.Tests/FocusBot.Infrastructure.Tests.csproj
dotnet test tests/FocusBot.Core.Tests/FocusBot.Core.Tests.csproj
```

**Requirements**: .NET 10 SDK, Windows 10/11, Windows App SDK.

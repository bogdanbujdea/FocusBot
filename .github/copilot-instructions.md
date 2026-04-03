# Foqus — GitHub Copilot Instructions

## Project Overview

Foqus is a productivity platform that classifies your current focus context against a single active task and tracks alignment over time. It consists of:

- **Windows desktop app** (WinUI 3 / .NET 10) — foreground window monitoring, focus scoring, overlay — [docs/desktop-app.md](../docs/desktop-app.md)
- **Browser extension** (Chrome/Edge, Manifest V3, TypeScript/React/Vite) — page/tab classification, distraction overlay — [docs/browser-extension.md](../docs/browser-extension.md)
- **WebAPI** (ASP.NET Core Minimal APIs, PostgreSQL) — auth, classification, sessions, subscriptions, analytics — [docs/webapi.md](../docs/webapi.md)
- **Web app** (`foqus-web-app`, React 19/Vite) — cloud dashboard at `app.foqus.me` — [docs/web-app.md](../docs/web-app.md)
- **Website** (`foqus-website`, React 19/Vite) — marketing landing page at `foqus.me` — [docs/website.md](../docs/website.md)

Platform overview: [docs/platform-overview.md](../docs/platform-overview.md). Integration guide: [docs/integration.md](../docs/integration.md).

---

## Solution Structure

| Project | Target | Purpose |
|---|---|---|
| `FocusBot.Core` | `net10.0` | Domain entities, interfaces, events. No external dependencies. |
| `FocusBot.Infrastructure` | `net10.0-windows` | Data access (EF Core/SQLite), Win32 services, Supabase auth, LLM providers |
| `FocusBot.App.ViewModels` | `net10.0` | Presentation logic (CommunityToolkit.Mvvm) |
| `FocusBot.App` | `net10.0-windows` | WinUI 3 UI, DI wiring, XAML |
| `FocusBot.WebAPI` | `net10.0` | Minimal API, vertical slice architecture, PostgreSQL |
| `browser-extension/` | TypeScript/React/Vite | Chrome Manifest V3 extension |
| `src/foqus-web-app/` | React 19/Vite/Recharts | Cloud dashboard SPA |
| `src/foqus-website/` | React 19/Vite | Marketing landing page |

No `.sln` file — build individual `.csproj` files. `TreatWarningsAsErrors` is on for all C# projects.

---

## Architecture

### Clean Architecture (Desktop App)

```
Core (no deps) → Infrastructure (Core only) → ViewModels (Core only) → App (DI wiring)
```

- **Core**: Entities, interfaces, domain events. Zero project references.
- **Infrastructure**: Depends only on Core. Implements interfaces (EF Core repos, Win32 services, external API clients).
- **ViewModels**: Depends only on Core interfaces. Uses CommunityToolkit.Mvvm source generators.
- **App**: Wires everything via DI. References all other projects.

### Vertical Slice Architecture (WebAPI)

Each feature has its own folder under `Features/`:

```
Features/<Name>/
├── SLICE.md            # Documentation
├── <Name>Endpoints.cs  # Minimal API route definitions
├── <Name>Service.cs    # Business logic
└── <Name>Dtos.cs       # Request/response DTOs
```

Existing slices: **Auth**, **Analytics**, **Classification**, **Clients**, **Pricing**, **Sessions**, **Subscriptions**, **Waitlist**.

### Browser Extension Architecture

```
UI (React popup/sidepanel/options) → Runtime (background/index.ts service worker) → Services (17 shared modules)
```

- Background service worker (~1174 lines) is the central state manager and classification orchestrator.
- State stored in `chrome.storage.local`. Classification cache in IndexedDB (SHA-256 keyed).
- Content script injected into tabs for the distraction overlay.
- Exclusive state mutations via `runExclusive()` promise queue.

### Web App Architecture

```
main.tsx → BrowserRouter → AuthProvider → ProtectedRoute → Layout → SubscriptionProvider → Page
```

- Supabase auth + SignalR real-time updates + Paddle.js checkout overlay.
- API client with automatic JWT injection and 401 token refresh/retry.
- 10 routes (5 public, 5 protected).

---

## C# Coding Rules

Full reference: [docs/coding-guidelines.md](../docs/coding-guidelines.md).

### Style

- **File-scoped namespaces** — single line, no braces.
- **Nullable reference types enabled** — be explicit: `string.Empty` for non-null, `string?` for nullable. No ambiguous nullability.
- **Expression-bodied members** for simple single-expression methods/properties.
- **Braces** required for multi-line blocks; optional for single-line statements.

### Return Types

- **No tuple returns.** Always use named types (records, classes).
  ```csharp
  // Bad: (string ProcessName, string Title)
  // Good: record ForegroundWindowInfo(string ProcessName, string WindowTitle);
  ```
- **No null for expected failure.** Use `Result<T>` from CSharpFunctionalExtensions.
  ```csharp
  // Bad: return null;
  // Good: return Result.Failure<AlignmentResult>("API key not configured");
  ```
- **Exceptions** only for programmer errors (invalid args, violated preconditions) or unrecoverable failures. Use `Result<T>` for expected/recoverable failures.

### Methods and Classes

- Methods: target 10–20 lines, max ~30. Extract helpers when longer.
- Classes: target 100–150 lines, max ~200. Single responsibility.
- Extract complex conditions into **named methods** describing intent (`HasActiveTask()`, `StopMonitoringAndResetFocusState()`).
- Use **guard clauses** (early returns) to reduce nesting.
- Prefer **step-by-step assignments** over deeply nested calls.
- Use descriptive variable names; avoid abbreviations.

### Dependency Injection

- Constructor injection with interfaces. Primary constructors OK for simple cases.
  ```csharp
  public class TaskRepository(AppDbContext context) : ITaskRepository { }
  ```

### ViewModels

- Use **CommunityToolkit.Mvvm** source generators: `[ObservableProperty]`, `[RelayCommand]`, `[NotifyPropertyChangedFor]`.
- Prevent double-fire: `[RelayCommand(AllowConcurrentExecutions = false)]`.

### Async

- Suffix async methods with `Async`.
- Accept `CancellationToken` (default parameter OK).
- Fire-and-forget: `_ = LoadBoardAsync();` — log exceptions in continuations.

### Documentation

- XML docs on all public classes/interfaces and public methods with non-obvious behavior.
- **No emojis** in code comments.
- Prefer self-documenting code; comment only when the code cannot explain itself.

### Pre-Commit Checklist

- [ ] No tuple returns
- [ ] No null for expected failure (use Result)
- [ ] Methods < 30 lines
- [ ] Classes < 200 lines
- [ ] Complex logic in named methods
- [ ] Descriptive variable names
- [ ] `Async` suffix on async methods
- [ ] Public API has XML docs
- [ ] No emojis in comments
- [ ] No nullable warnings (TreatWarningsAsErrors is on)

---

## TypeScript / React Rules

Full reference: [docs/coding-guidelines.md](../docs/coding-guidelines.md).

### Style

- **Strict TypeScript** (`strict: true`). No `any` unless truly unavoidable.
- **ESM modules** (`"type": "module"`). Use `import` / `export`.
- **Functional components** only — no class components.
- **Named exports** preferred over default exports.

### Naming

- Components: PascalCase (`SessionCard`, `FocusGauge`)
- Hooks: `use` prefix (`useAuth`, `usePaddle`)
- Files: PascalCase for components (`.tsx`), camelCase for utilities (`.ts`)
- Constants: UPPER_SNAKE_CASE (`APP_KEYS`)

### Patterns

- `useState` + `useEffect` for local state; `chrome.storage.local` for extension; React Context for cross-component.
- Extract reusable logic into hooks. Always clean up subscriptions/timers in effect returns.
- API calls return structured results (`{ ok, data }` | `{ ok: false, status, error }`). No thrown exceptions for expected failures.
- Extension background SW has no DOM — use `chrome.storage.local`, not `localStorage`.

---

## Unit Testing Rules

Full reference: [docs/unit-testing.md](../docs/unit-testing.md).

### C# Tests (xUnit + Awesome Assertions)

- Organized **per method/behavior**, not per class.
- Folder: name of type under test (e.g., `TaskRepositoryTests/`).
- File: `MethodNameShould.cs` or `BehaviorNameShould.cs`.
- Class: same as filename without extension.

### Naming

- Test class: `MethodNameShould` or `BehaviorNameShould`.
- Test method: `Result_WhenCondition` or `DoSomething_WhenCondition`.
  ```
  ReturnTask_WhenIdExists
  ReturnNull_WhenIdDoesNotExist
  SetActiveAsync_MakesTaskActive
  ```

### AAA Pattern

Every test has three sections with **explicit comments**:
```csharp
[Fact]
public async Task ReturnTask_WhenIdExists()
{
    // Arrange
    var created = await Repository.AddTaskAsync("Find me");

    // Act
    var found = await Repository.GetByIdAsync(created.TaskId);

    // Assert
    found.Should().NotBeNull();
    found!.TaskId.Should().Be(created.TaskId);
}
```

### Principles

- **One behavior per test** — one reason to fail.
- **Deterministic** — no real time, randomness, network, or environment dependencies. Use fakes.
- **Fast** — unit tests in milliseconds.
- **Isolated** — no order dependency, no shared mutable state.
- **No logic in tests** — no `if`, loops, or non-trivial expressions.
- **Test behavior, not implementation** — assert inputs, outputs, observable side effects.

### Assertions

- C#: **Awesome Assertions** (`.Should()`) for all assertions. Do not use `Assert.*`.
- TypeScript: Vitest `expect` + `@testing-library/jest-dom` matchers for DOM.

### Test Projects

| Project | ~Tests | Notes |
|---|---|---|
| `FocusBot.Core.Tests` | 25 | Entity behavior, domain logic |
| `FocusBot.WebAPI.Tests` | 82 | Service logic, webhook idempotency, security |
| `FocusBot.WebAPI.IntegrationTests` | 32 | WebApplicationFactory + InMemory DB, test JWT |
| `FocusBot.Infrastructure.Tests` | ~10 | Windows-only, data access, Win32 services |
| `FocusBot.App.ViewModels.Tests` | ~20 | Windows-only, ViewModel behavior |
| `browser-extension/tests/` | 83 | Vitest — analytics, storage, API client, metrics |
| `foqus-web-app/src/**/*.test.*` | 58 | Vitest — components, pages, API client, utils |

---

## WinUI 3 Design Rules

### Materials and Elevation

- **Glassy UI**: Semi-transparent blurred surfaces (Acrylic) for Light/Dark; solid colors for High Contrast.
- **3 elevation layers** (Store-critical):
  - Page (darkest): Dark `#110E1A` / Light `#EDE9F5`
  - Column (medium): Dark `#1C1730` / Light `#F5F3FA`
  - Card (lightest): Dark `#2A2242` / Light `#FFFFFF`
- Use **theme resources** (Fb* Color/Brush resources), not hardcoded hex in views.

### Theme File Layout

- `Themes/Colors.xaml` — ThemeDictionaries with `Fb*` Color resources only.
- `Themes/Brushes.xaml` — Semantic brushes; AcrylicBrush in Light/Dark, SolidColorBrush in HighContrast.
- `Themes/Styles.xaml` — Shared styles consuming semantic brushes.
- Merge in `App.xaml` in order: Colors → Brushes → Styles.

### Accessibility

- **Never encode meaning with color alone** — pair with icon, text label, or accent bar.
- Preserve **focus indicators** and **keyboard navigation**.
- Use `AutomationProperties.Name` on interactive elements.
- High Contrast: solid backgrounds only, 3 distinct layers, readable text.

### UX Decisions (Locked)

- **Hybrid**: Main window for setup and review; floating overlay for live focus status.
- **Passive by default**: Status via overlay color, no interruption. Optional toast for prolonged misalignment (configurable).
- **Default state**: Neutral until classification runs. Green only when clearly aligned.

### Design Tokens

- Corner radius: Small 6px, Medium 10px, Large 12px.
- Spacing: page margin 32px, column 16px, card 10px, card padding 12–14px.
- Typography: Segoe UI Variable; column headers 12px SemiBold uppercase; task titles 14px Medium.

---

## CSS Rules (Extension & Web App UI)

### Primary Goals

- **No duplicated CSS** — scan for existing selectors before adding new ones.
- **No unintended overrides** — scope new styles, don't change existing UI unless requested.
- **Match desktop aesthetic** — dark, glassy, 3 elevation layers, subtle borders.

### Architecture

- Scope page styles under a wrapper (`.settings-page ...`).
- Use component/page-prefixed classes (BEM-ish): `.settings-section`, `.settings-radio-card`.
- Avoid global element selectors for styling.
- Keep specificity low — prefer `.class` over nested chains.
- Avoid `!important` unless fixing third-party/UA issues.

### Design Alignment

- 3 surface layers: page (darkest) < section < card (lightest).
- Dark palette: Page `#110E1A`, Section `#1C1730`, Card `#2A2242`.
- Borders: 1px with low-alpha white (`rgba(255,255,255,0.08–0.18)`).
- Define **CSS variables** on the page wrapper for consistent color/elevation.
- Always provide visible `:focus-visible` / `:focus-within` indicators.

### Before Finishing

- [ ] No duplicate selectors added
- [ ] New styles are scoped (wrapper + component classes)
- [ ] Specificity is minimal
- [ ] Focus states are visible
- [ ] Colors/elevation match desktop (3 layers, subtle borders, glassy feel)

---

## Domain Concepts

### Core Model

- **Task**: A single user-defined focus objective (title + optional hints).
- **Session**: A time-bounded focus period. Starts when a task is defined, ends explicitly or by timeout. Contains aligned/distracted time, focus score, and metadata.
- **Classification**: A single evaluation of whether the current context (window title, URL, page title) aligns with the active task. Returns `Aligned`, `NotAligned`, or `Unclear`.
- **Focus Score**: A time-weighted alignment percentage (0–100) over a session.

### Classification Modes

- **BYOK (Bring Your Own Key)**: Client calls LLM provider directly using a user-provided API key. Desktop app supports OpenAI, Anthropic, Google via LlmTornado. Extension supports OpenAI only.
- **Managed (Foqus Account)**: Client calls WebAPI `POST /classify`, backend uses a platform-managed API key. Subscription/trial gated.

### Subscription Tiers (MVP)

| Tier | Key Source | Analytics | Sync |
|---|---|---|---|
| Free (BYOK) | User-provided | Basic (local) | None |
| Foqus BYOK | User-provided | Full (web app) | Yes |
| Foqus Premium | Platform-managed | Full (web app) | Yes |

### Analytics Split

- **Basic analytics** (all users, local): session list, duration, aligned vs distracted time, focus score, simple counts, last 7 days.
- **Full analytics** (paid users, web app only): trends, multi-device view, charts, filters, comparisons, cloud history.
- Native clients do NOT duplicate full analytics UI — they link to `app.foqus.me/analytics`.

### Integration Model

- **Device**: A registered client installation (Windows app or extension). Has type, fingerprint, name, version, last-seen timestamp.
- **Heartbeat**: Periodic signal (60s interval) from device to backend for presence tracking.
- **Local WebSocket**: `ws://localhost:9876/focusbot` — same-machine desktop ↔ extension shared task sync.
- **SignalR Hub**: `/hubs/focus` — server-mediated cross-machine sync for cloud users (coexists with local WebSocket).

### Auth

- All clients use Supabase magic-link authentication (same project).
- Desktop app: deep link callback (`foqus://auth-callback`).
- Extension: options page callback via Supabase JS.
- WebAPI: validates Supabase JWT (ES256, JWKS). Auto-provisions user from JWT `sub` claim.

---

## Key Technical Patterns

- **Result pattern**: `Result<T>` from CSharpFunctionalExtensions for all service returns. No null for failure.
- **Classification caching**: SHA-256 hash of context. Client-side (SQLite / IndexedDB, no expiry) + server-side (`ClassificationCache` table, 24h TTL).
- **Idle detection**: Desktop pauses after 5min of inactivity (Win32 `GetLastInputInfo`). Extension uses `chrome.idle` API.
- **Vertical slices**: Each WebAPI feature is self-contained with endpoints, service, DTOs, and documentation.
- **One active session per user**: Enforced server-side. Desktop ↔ extension use WebSocket for local conflict resolution.

---

## Building and Running

- **.NET**: `dotnet build src/FocusBot.WebAPI/FocusBot.WebAPI.csproj` (no .sln; build individual .csproj files)
- **Browser extension**: `cd browser-extension && npm run build`
- **Web app**: `cd src/foqus-web-app && npm run build`
- **Website**: `cd src/foqus-website && npm run build`
- **WebAPI dev**: Start PostgreSQL via Docker, then `dotnet run --project src/FocusBot.WebAPI/FocusBot.WebAPI.csproj --launch-profile http`
- **Extension dev**: `cd browser-extension && npm run dev`
- **Web app dev**: `cd src/foqus-web-app && npm run dev` (port 5174)

### Running Tests

```
dotnet test tests/FocusBot.Core.Tests/FocusBot.Core.Tests.csproj
dotnet test tests/FocusBot.WebAPI.Tests/FocusBot.WebAPI.Tests.csproj
dotnet test tests/FocusBot.WebAPI.IntegrationTests/FocusBot.WebAPI.IntegrationTests.csproj
cd browser-extension && npm test
cd src/foqus-web-app && npm test
```

### Key Caveats

- .NET 10 SDK required.
- `TreatWarningsAsErrors` is on — nullable warnings and code style issues break builds.
- `Microsoft.OpenApi` v2.x types are in `Microsoft.OpenApi` namespace, not `Microsoft.OpenApi.Models`.
- Docker needed for PostgreSQL locally. See `docker-compose.yml` or run the single container command in `AGENTS.md`.

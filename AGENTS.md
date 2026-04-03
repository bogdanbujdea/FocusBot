# AGENTS.md

## Autonomous Agent Instructions

Architecture: [docs/platform-overview.md](docs/platform-overview.md). Code conventions: [docs/coding-guidelines.md](docs/coding-guidelines.md). Testing: [docs/unit-testing.md](docs/unit-testing.md). Per-project docs: [desktop-app](docs/desktop-app.md), [webapi](docs/webapi.md), [browser-extension](docs/browser-extension.md), [web-app](docs/web-app.md), [website](docs/website.md). Integration: [docs/integration.md](docs/integration.md).

### Architecture overview

Foqus is a productivity platform: Windows desktop app + browser extension + Web API + web dashboard + marketing site. See `docs/platform-overview.md` for the full architecture and `docs/coding-guidelines.md` for code conventions.

| Project | Target | Builds on Linux? |
|---|---|---|
| `FocusBot.Core` | `net10.0` | Yes |
| `FocusBot.App.ViewModels` | `net10.0` | Yes |
| `FocusBot.WebAPI` | `net10.0` | Yes |
| `FocusBot.Infrastructure` | `net10.0-windows` | No |
| `FocusBot.App` (WinUI 3) | `net10.0-windows` | No |
| `FocusBot.Infrastructure.Tests` | `net10.0-windows` | No |
| `FocusBot.App.ViewModels.Tests` | `net10.0-windows` | No |
| `FocusBot.Core.Tests` | `net10.0` | Yes |
| `FocusBot.WebAPI.Tests` | `net10.0` | Yes |
| `FocusBot.WebAPI.IntegrationTests` | `net10.0` | Yes |
| `browser-extension` (Node/TS) | N/A | Yes |
| `foqus-web-app` (Vite/React) | N/A | Yes |
| `foqus-website` (Vite/React) | N/A | Yes |

### Running services

- **WebAPI**: Requires PostgreSQL. Start PG via `docker run -d --name focusbot-pg -e POSTGRES_DB=focusbot -e POSTGRES_USER=focusbot -e POSTGRES_PASSWORD=focusbot_dev -p 5432:5432 postgres:16-alpine`, then `dotnet run --project src/FocusBot.WebAPI/FocusBot.WebAPI.csproj --launch-profile http` (listens on `http://localhost:5251`). Auto-migrates the database on startup.
- **Web app dev**: `cd src/foqus-web-app && npm run dev` (listens on `http://localhost:5174`). Requires `.env` with `VITE_SUPABASE_URL` and `VITE_SUPABASE_ANON_KEY` for local dev (production defaults are built in for `import.meta.env.PROD`).
- **Browser extension dev**: `cd browser-extension && npm run dev`

### Paddle Billing (subscriptions)

- **Checkout hub**: Paid upgrades use **Paddle.js** on the web app billing page (`/billing`). Desktop and the browser extension open the same web URL; there are no separate `/checkout/*` routes.
- **WebAPI** (`FocusBot.WebAPI`): Configure the `Paddle` section in `appsettings.json` (`ApiBase`, `IsSandbox`, public `ClientToken`, **`CatalogProductId`** — the Paddle **`pro_...`** product whose prices appear on `/pricing`; use a different value per environment). Store **`ApiKey`** and **`WebhookSecret`** in user secrets or environment variables, for example:

  `dotnet user-secrets set "Paddle:ApiKey" "<sandbox-server-api-key>" --project src/FocusBot.WebAPI`

  `dotnet user-secrets set "Paddle:WebhookSecret" "<webhook-signing-secret>" --project src/FocusBot.WebAPI`

  `dotnet user-secrets set "Paddle:CatalogProductId" "pro_..." --project src/FocusBot.WebAPI`

  `dotnet user-secrets set "Paddle:ClientToken" "<client-side-token>" --project src/FocusBot.WebAPI` (from Paddle Dashboard → Developer tools → Authentication; required for Paddle.js on `/billing`)

- **Trial activation**: The **Foqus 24h trial** is created when the user is provisioned via **`GET /auth/me`** (`AuthService`), or defensively on `GET /subscriptions/status` if a `Users` row exists and no subscription row exists. The row uses `PlanType.TrialFullAccess` (= 0) and `TrialEndsAtUtc = UtcNow + 24h`. The web app calls **`GET /auth/me`** before **`GET /subscriptions/status`** (see `docs/web-app-sign-in-and-trials.md`). The explicit `POST /subscriptions/trial` still exists (accepts `planType` 0, 1, or 2 per API) but returns 409 if a row already exists. **Do not add a duplicate trial period on Paddle prices** if you use this app trial. There is no free plan — users are on trial or paid.
- **Subscription status**: Uses `SubscriptionStatus` enum (`None`, `Trial`, `Active`, `Expired`, `Canceled`). Serialized as camelCase strings in JSON responses (e.g., `"active"`, `"trial"`). `past_due` status maps to `Expired` (no access).
- **Webhook idempotency**: All events are deduplicated by `event_id` via the `ProcessedWebhookEvent` table. Duplicate Paddle retries are safely ignored.
- **Webhook security**: `PaddleWebhookVerifier` rejects all requests when `Paddle:WebhookSecret` is not configured or empty. No dev bypass.
- **Webhook URL** (local tunnel or deployed): `POST {apiBase}/subscriptions/paddle-webhook` — must receive the **raw** body for signature verification.
- **Realtime plan updates**: After webhook processing, the API emits **`PlanChanged`** on the same SignalR hub as focus sessions (`/hubs/focus`); desktop `FocusPageViewModel` and web clients can refresh subscription/plan state immediately.
- **Docs**: `docs/paddle-guide.md` (Foqus-specific section + generic Paddle Billing notes), `docs/paddle-implementation-summary.md` (detailed implementation), `docs/web-app-sign-in-and-trials.md` (sign-in, provisioning, trial vs Paddle `trialing`). See also `docs/webapi.md` (Subscriptions slice) and `docs/web-app.md` (billing page). Legacy Windows Store pricing notes are under `pricing/archive/`.

### Running tests

- **Core tests**: `dotnet test tests/FocusBot.Core.Tests/FocusBot.Core.Tests.csproj`
- **WebAPI unit tests**: `dotnet test tests/FocusBot.WebAPI.Tests/FocusBot.WebAPI.Tests.csproj` (82 tests, includes comprehensive webhook idempotency, race condition, and security coverage)
- **WebAPI integration tests**: `dotnet test tests/FocusBot.WebAPI.IntegrationTests/FocusBot.WebAPI.IntegrationTests.csproj` (32 tests, WebApplicationFactory + InMemory DB)
- **ViewModel tests**: `dotnet test tests/FocusBot.App.ViewModels.Tests/FocusBot.App.ViewModels.Tests.csproj`
- **Infrastructure tests**: `dotnet test tests/FocusBot.Infrastructure.Tests/FocusBot.Infrastructure.Tests.csproj`
- **Browser extension tests**: `cd browser-extension && npm test` (Vitest)
- **Web app tests**: `cd src/foqus-web-app && npm test` (Vitest, jsdom)
- Integration tests use `CustomWebApplicationFactory` which provides test JWT config and swaps Npgsql for InMemory DB.

### Building

- **.NET**: `dotnet build src/FocusBot.WebAPI/FocusBot.WebAPI.csproj` (no `.sln` file; build individual `.csproj` files)
- **Web app**: `cd src/foqus-web-app && npm run build`
- **Website**: `cd src/foqus-website && npm run build`
- **Browser extension**: `cd browser-extension && npm run build`

### Desktop app: focus sessions (API-only)

- Focus sessions are **not** stored in local SQLite. The desktop app uses the FocusBot Web API as the only source of truth for session lifecycle (`POST /sessions`, `GET /sessions/active`, `POST /sessions/{id}/end`). Users must be signed in; there is no offline session mode.
- The focus **board** (`FocusPageViewModel`) loads the active session from the API **after** Supabase auth is restored at startup, and **again** when authentication becomes available later (e.g. magic-link sign-in), so the UI matches the server’s in-progress session.
- Local SQLite (`AppDbContext`) retains **alignment cache** entries only; the `UserSessions` table was removed.
- `FocusBotApiClient` wraps session start/end calls with **Polly** retries: 3 attempts, 2 second delay between attempts, on transient HTTP failures (5xx, 408, `HttpRequestException`).
- **Cross-device session sync (desktop ↔ web)**: After sign-in, the desktop app connects to the same SignalR hub as the web dashboard (`IFocusHubClient` / `FocusHubClientService` → `{apiBaseUrl}/hubs/focus`, JWT via `access_token`). `FocusPageViewModel` reloads the active session from the API on `SessionStarted` / `SessionEnded` and mirrors pause/resume via `SessionPaused` / `SessionResumed` when the event matches the current session. The hub is disconnected on sign-out and on `ReAuthRequired`. Connection is established after `FocusPageViewModel` is created on cold start (so event handlers are subscribed before the first connect).

### Desktop app: trial UX and Foqus BYOK

- **`IPlanService`** (`FocusBot.Infrastructure` / `PlanService`) caches `GET /subscriptions/status` fields: `ClientPlanType`, `ClientSubscriptionStatus`, and `TrialEndsAt`. Use `GetStatusAsync()` / `GetTrialEndsAtAsync()` for UI that depends on trial end time, not only `GetCurrentPlanAsync()`.
- **Trial welcome** (`TrialWelcomeDialog`): After the first-run **How it works** dialog, signed-in users on the Foqus trial (`PlanType` trial tier + `status` trial) see a one-time welcome dialog unless `SettingsKeys.TrialWelcomeSeen` is set. **View plans** opens `https://app.foqus.me/billing`. Also shown when the user signs in after launch (via `AccountSection.PropertyChanged`).
- **Trial banner** (`FocusPage`): `InfoBar` with countdown and **Manage plan** (billing URL). Shown when the Foqus trial is active (`ClientPlanType.FreeBYOK` / server `TrialFullAccess`, `status` trial, future `trialEndsAt`). A separate **Subscription required** banner appears when the trial has ended locally (same tier, expired or past `trialEndsAt`) with **View plans**.
- **Foqus BYOK key prompt** (`BYOKKeyPromptDialog`): After `PlanChanged` / hub refresh, if the plan is `CloudBYOK` and no API key is stored in settings, the app prompts once per session to open **Settings** (DPAPI encryption copy in the dialog). **Later** dismisses for that session.
- **Web app post-checkout (Foqus BYOK)**: After Paddle `checkout.completed` for price `planType` **cloud-byok**, `/billing` shows `BYOKSetupModal` with steps to open the Windows app or extension and paste the API key in Settings.
- **Browser extension trial/banner behavior**: Popup `AppShell` shows a compact, non-dismissible trial banner only for Foqus trial (`status = trial`, `planType = 0`, future `trialEndsAt`), with **Manage plan** linking to `https://app.foqus.me/billing`. Banner is hidden in sidepanel.
- **Browser extension options subscription UI**: `options/main.tsx` uses a subscription summary (current plan label, end date, manage subscription link, refresh action) rather than plan comparison cards. After trial expiry without paid subscription, shows **No active plan** with **View plans**.
- **Browser extension BYOK prompt**: When plan is `cloud-byok` and no API key exists in settings, popup `AppShell` shows an **API key required** banner with **Learn more** and **Open settings** actions. The BYOK info dialog explains that keys are per-client (Windows app key does not sync to extension), stored in extension storage, sent directly to the AI provider over HTTPS, and never sent to Foqus servers. Options page highlights the API key field and shows the same security messaging.

### Client registration (API)

- The API treats a **client** as one registered **software install** (WinUI app, Chrome extension, Edge extension, etc.), not necessarily one physical machine. Each install has a stable **fingerprint** and a server-assigned **client id** (`POST /clients`, `PUT /clients/{id}/heartbeat`, `DELETE /clients/{id}`).
- **Startup**: `App.xaml.cs` subscribes to `AuthStateChanged` only **after** the initial `InitializeAuthAsync` completes, so `OnAuthStateChangedAsync` does not run twice on restore. `IClientService.EnsureClientIdLoadedAsync` hydrates the cached id from settings before deciding to register.
- Sessions and analytics use **`clientId`** (JSON: `clientId`). Analytics breakdown: `GET /analytics/clients`. Summary includes **`clientsActive`**.
- **Host** (`ClientHost`): `Unknown`, `Windows` (desktop), `Chrome`, `Edge` (extensions). **IpAddress** on the `Client` row is set from `HttpContext.Connection.RemoteIpAddress` on register and heartbeat.

### Key caveats

- .NET 10 SDK is required (installed at `/usr/share/dotnet`). The update script restores NuGet and npm packages.
- `TreatWarningsAsErrors` is on for all projects -- nullable warnings and code style issues break builds.
- `Microsoft.OpenApi` v2.x is used transitively (types are in `Microsoft.OpenApi` namespace, not `Microsoft.OpenApi.Models`).
- The Aspire projects (AppHost, ServiceDefaults) were removed in favor of standalone WebAPI with Docker Compose.
- Docker is needed to run PostgreSQL locally. See `docker-compose.yml` for full dev setup or use the single container command above.

# AGENTS.md

## Cursor Cloud specific instructions

### Architecture overview

Foqus is a Windows desktop productivity app + browser extension + Web API (vertical slice architecture). See `docs/coding-guidelines.md` for code conventions.

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

### Running services

- **WebAPI**: Requires PostgreSQL. Start PG via `docker run -d --name focusbot-pg -e POSTGRES_DB=focusbot -e POSTGRES_USER=focusbot -e POSTGRES_PASSWORD=focusbot_dev -p 5432:5432 postgres:16-alpine`, then `dotnet run --project src/FocusBot.WebAPI/FocusBot.WebAPI.csproj --launch-profile http` (listens on `http://localhost:5251`). Auto-migrates the database on startup.
- **Web app dev**: `cd src/foqus-web-app && npm run dev` (listens on `http://localhost:5174`). Requires `.env` with `VITE_SUPABASE_URL` and `VITE_SUPABASE_ANON_KEY` for local dev (production defaults are built in for `import.meta.env.PROD`).
- **Browser extension dev**: `cd browser-extension && npm run dev`

### Running tests

- **Core tests**: `dotnet test tests/FocusBot.Core.Tests/FocusBot.Core.Tests.csproj`
- **WebAPI unit tests**: `dotnet test tests/FocusBot.WebAPI.Tests/FocusBot.WebAPI.Tests.csproj` (49 tests, InMemory EF Core)
- **WebAPI integration tests**: `dotnet test tests/FocusBot.WebAPI.IntegrationTests/FocusBot.WebAPI.IntegrationTests.csproj` (32 tests, WebApplicationFactory + InMemory DB)
- **ViewModel tests**: `dotnet test tests/FocusBot.App.ViewModels.Tests/FocusBot.App.ViewModels.Tests.csproj`
- **Infrastructure tests**: `dotnet test tests/FocusBot.Infrastructure.Tests/FocusBot.Infrastructure.Tests.csproj`
- **Browser extension tests**: `cd browser-extension && npm test` (80 tests, Vitest)
- **Web app tests**: `cd src/foqus-web-app && npm test` (Vitest, jsdom)
- Integration tests use `CustomWebApplicationFactory` which provides test JWT config and swaps Npgsql for InMemory DB.

### Building

- **.NET**: `dotnet build src/FocusBot.WebAPI/FocusBot.WebAPI.csproj` (no `.sln` file; build individual `.csproj` files)
- **Web app**: `cd src/foqus-web-app && npm run build`
- **Browser extension**: `cd browser-extension && npm run build`

### Desktop app: focus sessions (API-only)

- Focus sessions are **not** stored in local SQLite. The desktop app uses the FocusBot Web API as the only source of truth for session lifecycle (`POST /sessions`, `GET /sessions/active`, `POST /sessions/{id}/end`). Users must be signed in; there is no offline session mode.
- The focus **board** (`FocusPageViewModel`) loads the active session from the API **after** Supabase auth is restored at startup, and **again** when authentication becomes available later (e.g. magic-link sign-in), so the UI matches the server’s in-progress session.
- Local SQLite (`AppDbContext`) retains **alignment cache** entries only; the `UserSessions` table was removed.
- `FocusBotApiClient` wraps session start/end calls with **Polly** retries: 3 attempts, 2 second delay between attempts, on transient HTTP failures (5xx, 408, `HttpRequestException`).

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

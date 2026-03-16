# AGENTS.md

## Cursor Cloud specific instructions

### Architecture overview

FocusBot is a Windows desktop productivity app + browser extension + Web API (vertical slice architecture). See `docs/coding-guidelines.md` for code conventions.

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

### Running services

- **WebAPI**: Requires PostgreSQL. Start PG via `docker run -d --name focusbot-pg -e POSTGRES_DB=focusbot -e POSTGRES_USER=focusbot -e POSTGRES_PASSWORD=focusbot_dev -p 5432:5432 postgres:16-alpine`, then `dotnet run --project src/FocusBot.WebAPI/FocusBot.WebAPI.csproj --launch-profile http` (listens on `http://localhost:5251`). Auto-migrates the database on startup.
- **Browser extension dev**: `cd browser-extension && npm run dev`

### Running tests

- **Core tests**: `dotnet test tests/FocusBot.Core.Tests/FocusBot.Core.Tests.csproj` (25 tests)
- **WebAPI unit tests**: `dotnet test tests/FocusBot.WebAPI.Tests/FocusBot.WebAPI.Tests.csproj` (29 tests, InMemory EF Core)
- **WebAPI integration tests**: `dotnet test tests/FocusBot.WebAPI.IntegrationTests/FocusBot.WebAPI.IntegrationTests.csproj` (6 tests, WebApplicationFactory + InMemory DB)
- **Browser extension tests**: `cd browser-extension && npm test` (76 tests, Vitest)
- Integration tests use `CustomWebApplicationFactory` which provides test JWT config and swaps Npgsql for InMemory DB.

### Building

- **.NET**: `dotnet build src/FocusBot.WebAPI/FocusBot.WebAPI.csproj` (no `.sln` file; build individual `.csproj` files)
- **Browser extension**: `cd browser-extension && npm run build`

### Key caveats

- .NET 10 SDK is required (installed at `/usr/share/dotnet`). The update script restores NuGet and npm packages.
- `TreatWarningsAsErrors` is on for all projects -- nullable warnings and code style issues break builds.
- `Microsoft.OpenApi` v2.x is used transitively (types are in `Microsoft.OpenApi` namespace, not `Microsoft.OpenApi.Models`).
- The Aspire projects (AppHost, ServiceDefaults) were removed in favor of standalone WebAPI with Docker Compose.
- Docker is needed to run PostgreSQL locally. See `docker-compose.yml` for full dev setup or use the single container command above.

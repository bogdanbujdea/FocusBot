# AGENTS.md

## Cursor Cloud specific instructions

### Architecture overview

FocusBot is a Windows desktop productivity app + browser extension + nascent Web API. See `docs/coding-guidelines.md` for architecture layers and code conventions.

| Project | Target | Builds on Linux? |
|---|---|---|
| `FocusBot.Core` | `net10.0` | Yes |
| `FocusBot.App.ViewModels` | `net10.0` | Yes |
| `FocusBot.WebAPI` | `net10.0` | Yes |
| `FocusBot.ServiceDefaults` | `net10.0` | Yes |
| `FocusBot.AppHost` (Aspire) | `net10.0` | Yes |
| `FocusBot.Infrastructure` | `net10.0-windows` | No |
| `FocusBot.App` (WinUI 3) | `net10.0-windows` | No |
| `FocusBot.Infrastructure.Tests` | `net10.0-windows` | No |
| `FocusBot.App.ViewModels.Tests` | `net10.0-windows` | No |
| `FocusBot.Core.Tests` | `net10.0` | Yes |
| `browser-extension` (Node/TS) | N/A | Yes |

### Running services

- **WebAPI**: `dotnet run --project src/FocusBot.WebAPI/FocusBot.WebAPI.csproj --launch-profile http` (listens on `http://localhost:5251`). Test with `curl http://localhost:5251/WeatherForecast`.
- **Browser extension dev**: `cd browser-extension && npm run dev`

### Running tests

- **.NET Core tests**: `dotnet test tests/FocusBot.Core.Tests/FocusBot.Core.Tests.csproj` (25 tests, xUnit)
- **WebAPI unit tests**: `dotnet test tests/FocusBot.WebAPI.Tests/FocusBot.WebAPI.Tests.csproj` (xUnit, InMemory EF Core)
- **WebAPI integration tests**: `dotnet test tests/FocusBot.WebAPI.IntegrationTests/FocusBot.WebAPI.IntegrationTests.csproj` (xUnit, `WebApplicationFactory` with InMemory DB)
- **Browser extension tests**: `cd browser-extension && npm test` (66 tests, Vitest)
- The Windows-targeted test projects (`FocusBot.Infrastructure.Tests`, `FocusBot.App.ViewModels.Tests`) cannot run on Linux.
- Integration tests use `CustomWebApplicationFactory` which swaps Npgsql for InMemory. The factory manually registers `ApiDbContext` as scoped (instead of using `AddDbContext`) to avoid dual-provider conflicts with Npgsql services that survive `AddDbContext` replacement.

### Building

- **.NET (Linux-compatible projects)**: `dotnet build src/FocusBot.WebAPI/FocusBot.WebAPI.csproj` or build individual projects. There is no `.sln` file.
- **Browser extension**: `cd browser-extension && npm run build`

### Infrastructure (Terraform + CI/CD)

- Terraform configs live in `infra/terraform/`. Validate with `terraform init -backend=false && terraform validate` (backend requires Azure credentials).
- CI/CD workflow at `.github/workflows/ci.yml` runs build + tests on push/PR, deploys to Azure on `main` push.
- See `infra/terraform/README.md` for deployment prerequisites and step-by-step instructions.

### Key caveats

- .NET 10 SDK is required (installed at `/usr/share/dotnet`). The VM snapshot includes the SDK; the update script restores NuGet and npm packages only.
- No `.sln` file exists; build/restore individual `.csproj` files.
- The Aspire AppHost (`FocusBot.AppHost`) can orchestrate the WebAPI, but for simple testing you can run the WebAPI project directly.
- `EnforceCodeStyleInBuild` is enabled in `Directory.Build.props` and `TreatWarningsAsErrors` is on for most projects - code style violations will break builds.

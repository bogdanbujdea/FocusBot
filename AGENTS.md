# FocusBot - Agent Instructions

## Cursor Cloud specific instructions

### Project overview

FocusBot is a Windows desktop productivity app (WinUI 3 / .NET 10 / Clean Architecture). It is a single solution (`FocusBot.slnx`) with 4 source projects and 3 test projects. See `docs/coding-guidelines.md` for architecture rules and `docs/unit-testing.md` for test conventions.

### Linux cloud environment constraints

This is a Windows-only desktop app (`net10.0-windows10.0.19041.0`). On the Linux cloud VM:

- **Cannot run** `FocusBot.App` (WinUI 3 requires Windows).
- **Can restore, build, and test** all other projects by passing `-p:EnableWindowsTargeting=true`.
- The only project that builds without the flag is `FocusBot.Core` and `FocusBot.App.ViewModels` (they target plain `net10.0`).

### Key commands

| Action | Command |
|---|---|
| Restore all packages | `dotnet restore FocusBot.slnx -p:EnableWindowsTargeting=true` |
| Build all (except App) | `dotnet build FocusBot.slnx -p:EnableWindowsTargeting=true` (App project will fail with NETSDK1032 — this is expected) |
| Run all tests | `dotnet test FocusBot.slnx -p:EnableWindowsTargeting=true` |
| Run Core tests only | `dotnet test tests/FocusBot.Core.Tests` |
| Run Infrastructure tests | `dotnet test tests/FocusBot.Infrastructure.Tests -p:EnableWindowsTargeting=true` |
| Run ViewModel tests | `dotnet test tests/FocusBot.App.ViewModels.Tests -p:EnableWindowsTargeting=true` |

### Chrome extension

The Chrome extension lives at `src/FocusBot.ChromeExtension/`. It is a Manifest V3 extension (no build step) that communicates with the desktop app via `localhost:51789`. Load it in Chrome via `chrome://extensions/` > Developer mode > Load unpacked.

To test the extension without the Windows desktop app, run a mock HTTP server that responds on `POST /api/browser-activity` (200 OK) and `GET /api/focus-state` (JSON with `status`, `taskName`, `reason`, `sessionElapsedSeconds`, `connected` fields).

### Gotchas

- The solution uses `.slnx` format (XML-based, not the classic `.sln`). Requires .NET 10 SDK.
- `FocusBot.App.csproj` sets a default `RuntimeIdentifier` of `win-x64` and restricts `Platforms` to `x86;x64;ARM64`. A full solution build on Linux will fail for this one project only — all libraries and tests succeed.
- `TreatWarningsAsErrors` is enabled in all projects. Any new warning will break the build.
- Tests use Awesome Assertions (`.Should()` syntax), not FluentAssertions. See `docs/unit-testing.md`.

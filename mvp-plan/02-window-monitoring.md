# Phase 2: Window Monitoring

**User Feature**: App detects which window is in the foreground while working on a task.

**End State**: When a task is InProgress, the app tracks foreground window changes and persists window context.

## What to Build

### Core Layer

- `WindowContext` entity: `ContextId` (hash), `ProcessName`, `WindowTitle`, `FirstSeen`, `LastSeen`
- `IWindowContextRepository` interface
- `IWindowMonitorService` interface

### Infrastructure Layer

- `WindowMonitorService` using Win32 `SetWinEventHook` for `EVENT_SYSTEM_FOREGROUND`
- `WindowContextRepository` for persisting window contexts
- `WindowChangeOrchestrator` coordinating window events
- Title-change detection (1-second polling for browser tab changes)
- Filter system processes and FocusBot windows

### App Layer

- Start/stop window monitoring when task enters/leaves InProgress
- Display current window info in UI (process name, title)

## Win32 APIs Used

```csharp
// P/Invoke declarations needed
SetWinEventHook(EVENT_SYSTEM_FOREGROUND, ...)
GetForegroundWindow()
GetWindowText(hwnd, ...)
GetWindowThreadProcessId(hwnd, out pid)
```

## Tests

- `WindowContextTests.cs` - ContextId generation
- `WindowMonitorService` tests (mocked Win32)
- Orchestrator tests

## Checklist

- [ ] Define `WindowContext` entity with `ContextId` hash generation
- [ ] Define `IWindowContextRepository` interface
- [ ] Define `IWindowMonitorService` interface
- [ ] Implement `WindowMonitorService` with Win32 P/Invoke
- [ ] Implement `WindowContextRepository`
- [ ] Implement `WindowChangeOrchestrator`
- [ ] Add title-change detection (1-second polling)
- [ ] Add system process filtering (SearchHost, ShellExperienceHost, etc.)
- [ ] Add FocusBot window filtering
- [ ] Start monitoring when task moves to InProgress
- [ ] Stop monitoring when task leaves InProgress
- [ ] Display current window info in UI
- [ ] Write `WindowContextTests.cs`
- [ ] Write orchestrator tests

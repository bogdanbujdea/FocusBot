# Phase 4: Time Tracking

**User Feature**: See how long you've been working on a task with a live timer, broken down by window.

**End State**: Timer auto-starts when task moves to InProgress, tracks time per window, persists across app restarts.

## What to Build

### Core Layer

- Add to `UserTask`: `StartTime`, `EndTime`, `TotalSecondsLogged`, `Paused`, `PausedAt`
- `WindowTimeEntry` entity: `Id`, `ContextKey`, `TaskId`, `StartTime`, `EndTime`, `LastHeartbeatUtc`
- `ITimeEntryRepository` interface

### Infrastructure Layer

- `TimeEntryRepository` for time entry CRUD
- Update `TaskRepository` with time-related methods (`AddLoggedTimeAsync`, `SetTaskPausedAsync`)
- Update `WindowChangeOrchestrator` to create/end time entries on window change
- Heartbeat mechanism (5-second updates for crash recovery)

### App Layer

- Live timer display on InProgress task card (updates every second)
- Timer format: `Xs`, `Xm Ys`, `Xh Ym Zs`
- Pause/Resume button on InProgress task
- Timer auto-starts on move to InProgress
- Timer auto-stops on move to ToDo or Done
- Time persists in `TotalSecondsLogged` across sessions
- On app exit: pause active task
- Task edit dialog shows window time breakdown (which apps were used)

## Crash Recovery (on app startup)

- Close dangling entries (set `EndTime = LastHeartbeatUtc`)
- Pause InProgress tasks that weren't properly stopped

## UI Mockup

```
┌─────────────────────────────────────────────────────────┐
│   In Progress                                           │
│   ┌─────────────────────────────┐                       │
│   │ Write documentation         │                       │
│   │ ⏱ 00:45:23  [⏸ Pause]       │                       │
│   └─────────────────────────────┘                       │
└─────────────────────────────────────────────────────────┘
```

## Tests

- Timer lifecycle tests (start, pause, resume, stop)
- Time accumulation across sessions
- `WindowTimeEntryTests.cs` - duration calculation

## Checklist

- [ ] Add time-related properties to `UserTask` entity
- [ ] Define `WindowTimeEntry` entity
- [ ] Define `ITimeEntryRepository` interface
- [ ] Implement `TimeEntryRepository`
- [ ] Add time-related methods to `TaskRepository`
- [ ] Update `WindowChangeOrchestrator` to manage time entries
- [ ] Implement heartbeat mechanism (5-second interval)
- [ ] Add live timer display to InProgress task card
- [ ] Implement 1-second UI ticker
- [ ] Add Pause/Resume button
- [ ] Implement timer auto-start on status change
- [ ] Implement timer auto-stop on status change
- [ ] Handle app exit (pause active task)
- [ ] Implement crash recovery on startup
- [ ] Add window time breakdown to task edit dialog
- [ ] Write timer lifecycle tests
- [ ] Write `WindowTimeEntryTests.cs`

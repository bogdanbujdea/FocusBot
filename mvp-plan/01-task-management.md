# Phase 1: Task Management (Kanban Board)

**User Feature**: Create, edit, delete tasks and move them between To Do, In Progress, and Done columns.

**End State**: A working Kanban board app with persistent task storage.

## What to Build

### Core Layer (`FocusBot.Core`)

- `UserTask` entity with `TaskId`, `Description`, `Status` (enum: ToDo, InProgress, Done), `CreatedAt`
- `ITaskRepository` interface for CRUD operations

### Infrastructure Layer (`FocusBot.Infrastructure`)

- `AppDbContext` with SQLite in `%LOCALAPPDATA%\FocusBot\`
- `TaskRepository` implementing `ITaskRepository`
- EF Core migrations

### App Layer (`FocusBot.App`)

- WinUI 3 packaged app shell with single page
- Kanban board UI with 3 columns (To Do, In Progress, Done)
- Task cards with description display
- Add task button and input dialog
- Edit/delete task via context menu or buttons
- Drag-and-drop between columns
- Only one task allowed in InProgress at a time (auto-move previous to ToDo)

## UI Mockup

```
┌─────────────────────────────────────────────────────────┐
│  [+ Add Task]                                           │
├─────────────────────────────────────────────────────────┤
│   To Do          │   In Progress   │   Done            │
│   ───────────    │   ───────────   │   ──────          │
│   ┌─────────┐    │   ┌─────────┐   │   ┌─────────┐     │
│   │ Task 1  │    │   │ Task 2  │   │   │ Task 3  │     │
│   └─────────┘    │   └─────────┘   │   └─────────┘     │
└─────────────────────────────────────────────────────────┘
```

## Tests

- `UserTaskTests.cs` - entity validation
- `TaskRepositoryTests.cs` - CRUD operations, status transitions

## Checklist

- [ ] Create solution with FocusBot.Core, FocusBot.Infrastructure, FocusBot.App projects
- [ ] Define `UserTask` entity and `TaskStatus` enum
- [ ] Define `ITaskRepository` interface
- [ ] Set up `AppDbContext` with SQLite
- [ ] Implement `TaskRepository`
- [ ] Create initial EF Core migration
- [ ] Build WinUI 3 app shell
- [ ] Implement Kanban board UI with 3 columns
- [ ] Add task creation dialog
- [ ] Add task edit/delete functionality
- [ ] Implement drag-and-drop between columns
- [ ] Enforce single InProgress task rule
- [ ] Write `UserTaskTests.cs`
- [ ] Write `TaskRepositoryTests.cs`

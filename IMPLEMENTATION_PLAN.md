# FocusBot MVP: Detailed Implementation Plan

## Overview
Transform FocusBot from a Kanban-based multi-task system to a single-task flow with minimal data storage and history/analytics views. This achieves parity between the Windows app and browser extension while keeping the MVP simple for validation.

---

## Step 1: Replace Kanban Board with Single Task (Windows App)

### 1.1 Simplify KanbanBoardPage.xaml UI
**Goal:** Remove three-column layout, show only active task or start task form.

**Actions:**
- **Remove:**
  - Grid with three columns (To Do, In Progress, Done)
  - Column headers and separators
  - Task card templates for To Do and Done
  - Add task button in header
  - Edit task popup/flyout
  - Drag-and-drop handlers
  
- **Keep & Modify:**
  - Focus status bar (current app/window, classification, reason)
  - Daily analytics summary card
  - Extension connection indicator
  
- **Add:**
  - **Start Task Form** (visible when no active task):
    - TextBox for task title (required)
    - TextBox for context (optional, 200 char max) ✅ Already has MaxLength
    - "Start Task" button
    - Link/button to "View History"
  - **Active Task Card** (visible when task is in progress):
    - Task title and context
    - Elapsed time display
    - Focus score percentage (live)
    - "Pause" and "End Task" buttons
    - Extension sync status if applicable

**Files:**
- `src/FocusBot.App/Views/KanbanBoardPage.xaml`
- `src/FocusBot.App/Views/KanbanBoardPage.xaml.cs`

**Estimated Changes:** ~500 lines removed, ~150 lines added/modified

---

### 1.2 Simplify KanbanBoardViewModel
**Goal:** Remove collections and logic for To Do/Done tasks; keep only active task management.

**Actions:**
- **Remove Properties:**
  - `ToDoTasks` ObservableCollection
  - `DoneTasks` ObservableCollection
  - `ShowAddTaskInput`
  - `NewTaskDescription` / `NewTaskContext` (move to inline start form)
  - `ShowEditTaskInput`
  - `EditingTaskId`
  - `EditTaskDescription` / `EditTaskContext`
  
- **Remove Commands:**
  - `AddTaskCommand`
  - `CancelAddTaskCommand`
  - `EditTaskCommand`
  - `SaveTaskEditCommand`
  - `CancelEditTaskCommand`
  - `DeleteTaskCommand`
  - `MoveToInProgressCommand`
  - `MoveToToDoCommand`
  - `MoveToDoneCommand`
  - Drag-and-drop related commands
  
- **Keep & Modify:**
  - `InProgressTasks` → Rename to `ActiveTask` (single UserTask, not collection)
  - `DisplayInProgressTasks` → Keep for extension integration (shows local + remote task)
  - All focus monitoring, status bar, and integration logic
  - `RemoteTaskFromExtension` for extension sync
  
- **Add Properties:**
  - `ActiveTask` (UserTask?, null when no task)
  - `StartTaskTitle` (string, for start form)
  - `StartTaskContext` (string, for start form)
  - `ShowStartForm` (bool, true when no active task)
  
- **Add Commands:**
  - `StartTaskCommand` - Create task, set InProgress, start monitoring
  - `PauseTaskCommand` - Existing logic
  - `ResumeTaskCommand` - Existing logic
  - `EndTaskCommand` - Modified to compute summary before Done
  - `ViewHistoryCommand` - Navigate to History page

**Files:**
- `src/FocusBot.App.ViewModels/KanbanBoardViewModel.cs`

**Estimated Changes:** ~300 lines removed, ~100 lines modified, ~50 lines added

---

### 1.3 Update Task Repository
**Goal:** Simplify to support single active task + completed tasks only.

**Actions:**
- **Keep Methods:**
  - `GetInProgressTaskAsync()` - Returns single task or null
  - `CreateTaskAsync(UserTask)` - Creates with InProgress status
  - `UpdateTaskAsync(UserTask)` - Updates task
  - `GetDoneTasksAsync()` - For history page (with pagination)
  - `UpdateFocusScoreAsync(taskId, scorePercent)`
  - Focus segment and distraction event methods (for active task only)
  
- **Remove/Simplify Methods:**
  - `GetToDoTasksAsync()` - No longer needed
  - `SetStatusToAsync(taskId, status)` - Simplify to only InProgress→Done
  
- **Modify Behavior:**
  - Ensure only one InProgress task allowed (enforce at repository level)
  - When creating new task, any existing InProgress task is auto-ended

**Files:**
- `src/FocusBot.Core/Interfaces/ITaskRepository.cs`
- `src/FocusBot.Infrastructure/Data/TaskRepository.cs`

**Estimated Changes:** ~50 lines removed, ~20 lines modified

---

### 1.4 Update App Startup
**Goal:** Load only active task on startup, no To Do/Done lists.

**Actions:**
- In `KanbanBoardViewModel.InitializeAsync()`:
  - Remove loading of ToDo and Done collections
  - Load single in-progress task
  - If found, start monitoring and show active task UI
  - If not found, show start task form

**Files:**
- `src/FocusBot.App.ViewModels/KanbanBoardViewModel.cs` (constructor and Initialize)

**Estimated Changes:** ~30 lines removed, ~10 lines modified

---

## Step 2: Minimal Data Storage (Windows App)

### 2.1 Add Summary Fields to UserTask
**Goal:** Store computed summary when task ends, enable minimal storage approach.

**Actions:**
- **Add to UserTask entity:**
  ```csharp
  public long FocusedSeconds { get; set; }
  public long DistractedSeconds { get; set; }
  public int DistractionCount { get; set; }
  public int ContextSwitchCostSeconds { get; set; }
  public string? TopDistractingApps { get; set; } // JSON: [{app, seconds, count}]
  ```
  
- **Create migration:**
  - Add new columns to UserTask table
  - Set defaults for existing rows (0 for numbers, null for JSON)

**Files:**
- `src/FocusBot.Core/Entities/UserTask.cs`
- `src/FocusBot.Infrastructure/Migrations/` (new migration file)

**Estimated Changes:** ~10 lines to entity, ~50 lines for migration

---

### 2.2 Compute Summary on Task End
**Goal:** Calculate and persist summary metrics before marking task as Done.

**Actions:**
- **In `EndTaskCommand` / task completion logic:**
  1. Get all FocusSegments for task
  2. Get all DistractionEvents for task
  3. Compute:
     - `focusedSeconds` = sum(segments where score >= 6)
     - `distractedSeconds` = sum(segments where score < 4)
     - `distractionCount` = count(distraction events)
     - `contextSwitchCostSeconds` = sum(segments duration < 120 seconds)
     - `topDistractingApps` = top 10 apps by distracted seconds (JSON array)
  4. Set these on UserTask
  5. Persist UserTask with Status=Done
  6. **Delete** FocusSegments for this task
  7. **Delete** DistractionEvents for this task (or keep only for "today" if daily view needs it)

**Files:**
- `src/FocusBot.App.ViewModels/KanbanBoardViewModel.cs` (end task logic)
- Create new service: `src/FocusBot.Infrastructure/Services/TaskSummaryService.cs`
- Interface: `src/FocusBot.Core/Interfaces/ITaskSummaryService.cs`

**Estimated Changes:** ~150 lines new code, ~30 lines to wire up in ViewModel

---

### 2.3 Clean Up Segments and Events
**Goal:** Delete raw data after summarization to minimize storage.

**Actions:**
- **In TaskRepository:**
  - `DeleteFocusSegmentsForTaskAsync(taskId)` - Already exists ✅
  - `DeleteDistractionEventsForTaskAsync(taskId)` - Add if not exists
  
- **Call after task summary persisted:**
  ```csharp
  await _repo.DeleteFocusSegmentsForTaskAsync(taskId);
  await _repo.DeleteDistractionEventsForTaskAsync(taskId);
  ```

**Files:**
- `src/FocusBot.Core/Interfaces/ITaskRepository.cs`
- `src/FocusBot.Infrastructure/Data/TaskRepository.cs`
- `src/FocusBot.Core/Interfaces/IDistractionEventRepository.cs`
- `src/FocusBot.Infrastructure/Data/DistractionEventRepository.cs`

**Estimated Changes:** ~20 lines

---

### 2.4 Optional: Retain Events for "Today" Only
**Goal:** Keep DistractionEvents for current day for daily analytics, delete older.

**Actions:**
- Instead of deleting all events on task end:
  - Delete events older than today: `DeleteDistractionEventsBeforeAsync(DateOnly)`
  - Daily cleanup job (or on app startup): delete events >1 day old
  
- Daily analytics can use fresh events + completed task summaries

**Files:**
- Same as 2.3

**Estimated Changes:** ~30 lines (if implemented)

---

## Step 3: Add History Page (Windows App)

### 3.1 Create HistoryPage View
**Goal:** Show list of completed tasks with per-task and per-day analytics.

**Actions:**
- **Create XAML page:**
  - Top: Date range selector (Today / Last 7 Days / Last 30 Days / All)
  - Middle: ListView or DataGrid with columns:
    - Task Title
    - Completed Date/Time
    - Focus Score %
    - Total Time (formatted)
    - Distractions Count
  - Bottom: Summary card for selected range:
    - Total tasks completed
    - Average focus score
    - Total focused vs distracted time
    - Top distracting apps (from aggregated JSON)
  - Detail view (on task click):
    - Full task metrics
    - Context switch cost
    - Top distracting apps for that task
    - Timeline/chart (optional)

**Files:**
- `src/FocusBot.App/Views/HistoryPage.xaml`
- `src/FocusBot.App/Views/HistoryPage.xaml.cs`

**Estimated Changes:** ~300 lines new XAML/code-behind

---

### 3.2 Create HistoryViewModel
**Goal:** Load completed tasks, compute aggregates for selected range.

**Actions:**
- **Properties:**
  - `CompletedTasks` ObservableCollection<UserTask>
  - `SelectedRange` (enum: Today, Week, Month, All)
  - `FilteredTasks` (computed from CompletedTasks + SelectedRange)
  - `TotalFocusScore` (average of filtered)
  - `TotalFocusedSeconds`
  - `TotalDistractedSeconds`
  - `TotalDistractionCount`
  - `TopDistractingApps` (merge JSON from all filtered tasks)
  
- **Commands:**
  - `RefreshCommand` - Reload from DB
  - `ChangeRangeCommand` - Update filter
  - `ViewTaskDetailCommand` - Show detail flyout
  
- **Logic:**
  - Load Done tasks from repository
  - Filter by date range (compare CreatedAt or EndedAt)
  - Aggregate metrics by summing/averaging fields
  - Parse and merge `TopDistractingApps` JSON

**Files:**
- `src/FocusBot.App.ViewModels/HistoryViewModel.cs`

**Estimated Changes:** ~200 lines new code

---

### 3.3 Wire Up Navigation
**Goal:** Allow navigation from main view to History page.

**Actions:**
- Add "View History" button/link in main view (when no active task)
- Implement `ViewHistoryCommand` in KanbanBoardViewModel:
  ```csharp
  [RelayCommand]
  private void ViewHistory()
  {
      _navigationService.NavigateTo<HistoryPage>();
  }
  ```
  
- Register HistoryPage and HistoryViewModel in DI container

**Files:**
- `src/FocusBot.App/App.xaml.cs` (DI registration)
- `src/FocusBot.App.ViewModels/KanbanBoardViewModel.cs`

**Estimated Changes:** ~20 lines

---

### 3.4 Per-Day Aggregation
**Goal:** Show analytics grouped by day (not just per-task).

**Actions:**
- **In HistoryViewModel:**
  - Add `DailyStats` collection (date, focus%, tasks, time)
  - Group `FilteredTasks` by date(CompletedAt)
  - For each day:
    - Sum focusedSeconds, distractedSeconds
    - Count tasks
    - Average focus score
  - Display in chart or list

**Files:**
- `src/FocusBot.App.ViewModels/HistoryViewModel.cs`
- `src/FocusBot.App/Views/HistoryPage.xaml` (UI for daily view)

**Estimated Changes:** ~100 lines

---

## Step 4: Add History Page (Browser Extension)

### 4.1 Persist Only Summary on Session End
**Goal:** Stop storing full `visits[]` array long-term, keep only SessionSummary.

**Actions:**
- **Modify `endSession` in background/index.ts:**
  1. Compute SessionSummary (already exists via `calculateSessionSummary`)
  2. Create a **CompletedSession** object:
     ```typescript
     interface CompletedSession {
       sessionId: string;
       taskText: string;
       taskHints?: string;
       startedAt: string;
       endedAt: string;
       summary: SessionSummary; // includes all metrics
     }
     ```
  3. Save to `focusbot.completedSessions` array (new key)
  4. **Delete** `visits` before saving completed session
  5. Clear active session

- **Cap storage:**
  - Keep only last 100 completed sessions (or last 90 days)
  - Older sessions auto-pruned

**Files:**
- `browser-extension/src/shared/types.ts` (add CompletedSession)
- `browser-extension/src/shared/storage.ts` (add saveCompletedSession)
- `browser-extension/src/background/index.ts` (modify endSession)

**Estimated Changes:** ~80 lines

---

### 4.2 Create History UI Component
**Goal:** Show completed sessions with analytics.

**Actions:**
- **Create `HistoryPage.tsx`:**
  - Date range selector (Today / 7d / 30d)
  - List of completed sessions:
    - Task text
    - End time
    - Focus %
    - Tracked time
    - Distraction count
  - Summary card:
    - Total sessions
    - Average focus %
    - Total focused vs distracted time
    - Top distracting domains
  
- **Create `HistorySummaryCard.tsx`:**
  - Reusable component for per-day or per-session summary
  - Display metrics from CompletedSession.summary

**Files:**
- `browser-extension/src/ui/HistoryPage.tsx`
- `browser-extension/src/ui/HistorySummaryCard.tsx`

**Estimated Changes:** ~250 lines new code

---

### 4.3 Update Analytics to Use Completed Sessions
**Goal:** Derive analytics from `completedSessions` instead of full sessions with visits.

**Actions:**
- **Modify `calculateAnalytics` in analytics.ts:**
  - Load `CompletedSession[]` instead of `FocusSession[]`
  - Use `session.summary` fields directly (no need to loop through visits)
  - Aggregate by day using `endedAt` date
  - Extract `topDistractionDomains` from summaries

**Files:**
- `browser-extension/src/shared/analytics.ts`

**Estimated Changes:** ~50 lines modified

---

### 4.4 Add Navigation to History
**Goal:** Link from main UI to History page.

**Actions:**
- Add "History" link in `AppShell.tsx` navigation
- Register route in manifest or router (if using routing)
- Or: use conditional render in `sidepanel/index.tsx`:
  ```tsx
  const [view, setView] = useState<'main' | 'history'>('main');
  // Render HistoryPage when view === 'history'
  ```

**Files:**
- `browser-extension/src/ui/AppShell.tsx`
- `browser-extension/src/sidepanel/index.tsx`

**Estimated Changes:** ~30 lines

---

### 4.5 Per-Day Aggregation (Extension)
**Goal:** Show daily breakdown, not just list of sessions.

**Actions:**
- **Add `DailyBreakdown` component:**
  - Group completed sessions by date(endedAt)
  - For each day:
    - Show date
    - Count sessions
    - Sum focused/distracted seconds
    - Calculate day's focus %
    - Show top distracting domain for that day
  
- Render as expandable list or chart

**Files:**
- `browser-extension/src/ui/DailyBreakdown.tsx`
- `browser-extension/src/ui/HistoryPage.tsx`

**Estimated Changes:** ~100 lines

---

## Testing & Validation

### Manual Testing Checklist

**Windows App:**
- [ ] Start a new task (with and without context)
- [ ] Monitor switches app/window, see live classification
- [ ] End task, verify summary fields populated
- [ ] Check DB: FocusSegments and DistractionEvents deleted for completed task
- [ ] Open History page, see completed task
- [ ] View per-day analytics, verify aggregation
- [ ] Start new task while one is active (should auto-end previous)
- [ ] Extension integration: start task in extension, see in app

**Browser Extension:**
- [ ] Start session (with and without context)
- [ ] Navigate tabs, see classification
- [ ] End session, verify summary saved
- [ ] Check storage: `visits` not in completedSessions
- [ ] Open History, see completed session
- [ ] View per-day analytics
- [ ] Cap test: create 101 sessions, verify oldest deleted

**Cross-Platform:**
- [ ] Start in app, see in extension
- [ ] Start in extension, see in app
- [ ] End from app, extension clears
- [ ] End from extension, app clears
- [ ] Context hints passed app ↔ extension

---

## Risk Mitigation

### Data Migration
**Risk:** Existing users lose To Do tasks.

**Mitigation:**
- Auto-convert existing ToDo tasks to Done on first run (set EndedAt = now, FocusScore = null)
- Show one-time notice: "Task management has been simplified. Your completed tasks are preserved in History."

### Storage Quota
**Risk:** Chrome storage quota exceeded with too many completed sessions.

**Mitigation:**
- Cap at 100 completed sessions
- Implement pruning: delete sessions older than 90 days
- Monitor quota usage, warn user if >80%

### Performance
**Risk:** Loading all completed tasks at once is slow.

**Mitigation:**
- Paginate History view (load 50 at a time)
- Index by date in DB for fast range queries
- Lazy-load details (summary on expand)

---

## Rollout Plan

### Phase 1: Core Changes (Week 1)
- Step 1.1-1.4: Replace Kanban with single task
- Step 2.1-2.3: Minimal storage (app)
- Manual testing of core flow

### Phase 2: History (Week 2)
- Step 3.1-3.4: History page (app)
- Step 4.1-4.3: History page (extension)
- Manual testing of history and analytics

### Phase 3: Polish & Validation (Week 3)
- Step 3.4, 4.5: Per-day aggregation
- Integration testing
- User feedback session (5-10 users)

### Phase 4: Documentation & Release
- Update README with new flow
- Record demo video
- Release notes for breaking changes
- Deploy to stores

---

## Summary of Changes

| Component | Lines Removed | Lines Added | Lines Modified | New Files |
|-----------|---------------|-------------|----------------|-----------|
| **Windows App UI** | ~500 | ~150 | ~100 | 2 (History XAML) |
| **Windows App ViewModel** | ~300 | ~150 | ~150 | 2 (History VM, Summary Service) |
| **Windows App Data** | ~50 | ~80 | ~50 | 1 (Migration) |
| **Extension UI** | ~0 | ~350 | ~0 | 3 (History page, components) |
| **Extension Storage** | ~0 | ~80 | ~50 | 0 |
| **Extension Analytics** | ~0 | ~50 | ~50 | 0 |
| **Documentation** | ~50 | ~200 | ~100 | 1 (This plan) |
| **TOTAL** | ~900 | ~1,110 | ~500 | 9 |

**Net change:** ~710 lines added (mostly new History features)

---

## Success Criteria

- ✅ Both app and extension have same task flow (start → active → end)
- ✅ Both have task title + context (200 chars)
- ✅ API key storage documented as "safe as platform allows"
- ✅ Completed tasks show in History with analytics
- ✅ Per-task metrics: focus score, focused vs distracted time, distractions, context switch cost
- ✅ Per-day aggregation works
- ✅ Raw data (segments, events, visits) deleted after summarization
- ✅ Extension integration still works (shared task)
- ✅ Storage usage minimized (<5MB for 90 days history)
- ✅ All existing features preserved (monitoring, classification, idle detection, etc.)

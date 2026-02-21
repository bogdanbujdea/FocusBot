# Focus Score Feature

## Overview

The Focus Score measures how well a user stays focused on task-relevant applications while working. It provides real-time feedback as a percentage (0-100%) displayed on active task cards, helping users understand their focus patterns.

**User-facing description:**
> FocusBot tracks which applications you use while working on a task and uses AI to determine how relevant each app is. Your Focus Score shows the percentage of time spent in task-related apps, helping you stay on track.

## How It Works

### 1. Window Monitoring

When a task is "In Progress", FocusBot monitors foreground window changes:

1. User switches to a new window
2. FocusBot captures the **process name** and **window title**
3. A **context hash** is computed from these values (SHA256)
4. The AI classifies how aligned this window is with the current task (1-10 scale)
5. Time spent in that window is tracked and aggregated

### 2. AI Classification

The AI evaluates each window against the task description and returns an **alignment score** (1-10):

| Score | Meaning |
|-------|---------|
| 1-3 | Not aligned (distractions) |
| 4-6 | Partially aligned (neutral) |
| 7-10 | Highly aligned (productive) |

Classification results are cached by context hash to avoid redundant API calls.

### 3. Focus Score Calculation

The focus score is a **time-weighted average** of alignment scores, converted to a percentage:

```
Focus Score = (Sum of (AlignmentScore × DurationSeconds)) / TotalSeconds × 10
```

Example:
- 30 minutes in VS Code (score 9): 30 × 9 = 270
- 10 minutes in Slack (score 4): 10 × 4 = 40
- Total: (270 + 40) / 40 × 10 = **77.5%**

### 4. Aggregation Strategy

To prevent database bloat from frequent window switching, segments are aggregated by a unique key:

```
(TaskId, ContextHash, AlignmentScore)
```

This means:
- Same app + same window title + same score = **one database row** with accumulated duration
- Same app with different window titles = **separate rows** (e.g., different browser tabs)
- Same context with different scores = **separate rows** (e.g., task description changed mid-session)

## Architecture

### Layers

```
┌─────────────────────────────────────────────────────────┐
│  App (UI)                                               │
│  - KanbanBoardPage.xaml (displays focus score)          │
│  - Converters for formatting                            │
└─────────────────────────────────────────────────────────┘
                           │
┌─────────────────────────────────────────────────────────┐
│  App.ViewModels                                         │
│  - KanbanBoardViewModel                                 │
│    - Handles window change events                       │
│    - Triggers AI classification                         │
│    - Updates UI properties                              │
└─────────────────────────────────────────────────────────┘
                           │
┌─────────────────────────────────────────────────────────┐
│  Infrastructure                                         │
│  - FocusScoreService (singleton)                        │
│    - In-memory segment tracking                         │
│    - Score calculation                                  │
│    - Persistence via ITaskRepository                    │
│  - TaskRepository                                       │
│    - FocusSegment CRUD operations                       │
└─────────────────────────────────────────────────────────┘
                           │
┌─────────────────────────────────────────────────────────┐
│  Core                                                   │
│  - FocusSegment entity                                  │
│  - IFocusScoreService interface                         │
│  - HashHelper (context hashing)                         │
└─────────────────────────────────────────────────────────┘
```

### Key Components

#### FocusSegment Entity

```csharp
public class FocusSegment
{
    public int Id { get; set; }
    public string TaskId { get; set; }
    public string ContextHash { get; set; }      // SHA256 of "processName|windowTitle"
    public int AlignmentScore { get; set; }      // 1-10 from AI
    public int DurationSeconds { get; set; }     // Accumulated time
    public string? WindowTitle { get; set; }     // For display purposes
    public string? ProcessName { get; set; }     // For display purposes
}
```

#### IFocusScoreService Interface

```csharp
public interface IFocusScoreService
{
    // Start tracking with a known score (cache hit)
    void StartOrResumeSegment(string taskId, string contextHash, int alignmentScore,
        string? windowTitle, string? processName);
    
    // Start tracking before AI responds
    void StartPendingSegment(string taskId, string contextHash,
        string? windowTitle, string? processName);
    
    // Called when AI classification returns
    void UpdatePendingSegmentScore(int alignmentScore);
    
    // Stop tracking current segment
    void PauseCurrentSegment();
    
    // Calculate the focus score for a task
    int CalculateFocusScorePercent(string taskId);
    
    // Duration of current active segment
    int GetCurrentSegmentDurationSeconds();
    
    // True if we have at least one real AI-classified score
    bool HasRealScore { get; }
    
    // Save segments to database
    Task PersistSegmentsAsync();
    
    // Load segments from database (for resumed tasks)
    Task LoadSegmentsForTaskAsync(string taskId);
    
    // Clear in-memory segments for a task
    void ClearTaskSegments(string taskId);
}
```

## Implementation Details

### Pending Segments

When a user switches windows, the AI classification is asynchronous. The service handles this with a two-phase approach:

1. **StartPendingSegment**: Called immediately when window changes. Stores context info but does NOT add to segments dictionary yet.

2. **UpdatePendingSegmentScore**: Called when AI responds. Creates the actual segment with the real score and adds accumulated time.

This approach ensures:
- Score 5 is treated as a valid alignment score (not a special "pending" value)
- `HasRealScore` correctly indicates whether we have AI-classified data
- No time is tracked for segments that never receive a classification

### FocusBot Window Exclusion

The application ignores its own window to avoid tracking time spent in FocusBot itself:

```csharp
if (string.Equals(e.ProcessName, "FocusBot", StringComparison.OrdinalIgnoreCase))
    return; // Pause tracking, don't start new segment
```

### Service Lifetime

`FocusScoreService` is registered as a **singleton** because it maintains in-memory state across window changes. Since `ITaskRepository` is scoped, the service uses `IServiceScopeFactory` to create scopes for database operations:

```csharp
public async Task PersistSegmentsAsync()
{
    using var scope = _scopeFactory.CreateScope();
    var repo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
    await repo.UpsertFocusSegmentsAsync(toPersist);
}
```

### Persistence Timing

Segments are persisted to the database:
- Periodically during active tracking (every tick from time tracking service)
- When a task is moved to "Done"
- When a task is paused (moved back to "To Do")

### Context Hash Computation

Context hashes uniquely identify a window context:

```csharp
public static string ComputeWindowContextHash(string processName, string windowTitle)
{
    var normalized = NormalizeWindowTitle(windowTitle);  // Truncate to 200 chars
    return ComputeHash($"{processName}|{normalized}");   // SHA256
}
```

## Database Schema

### FocusSegments Table

| Column | Type | Description |
|--------|------|-------------|
| Id | INTEGER | Primary key |
| TaskId | TEXT(64) | Foreign key to UserTasks |
| ContextHash | TEXT(64) | SHA256 hash of context |
| AlignmentScore | INTEGER | 1-10 alignment score |
| DurationSeconds | INTEGER | Accumulated duration |
| WindowTitle | TEXT(512) | Original window title |
| ProcessName | TEXT(256) | Process name |

**Indexes:**
- Unique index on `(TaskId, ContextHash, AlignmentScore)` - enables upsert
- Index on `TaskId` - for loading task segments

### UserTasks Table (Extended)

| Column | Type | Description |
|--------|------|-------------|
| FocusScorePercent | INTEGER? | Final focus score (0-100), nullable |

## UI Display

### Active Task Card

Shows live-updating focus score while task is in progress:
- Only visible when `HasRealScore` is true (AI has returned at least one classification)
- Updates every second via time tracking tick
- Format: "Focus: 78%"

### Completed Task Card

Shows final focus score:
- Only visible when `FocusScorePercent` is not null
- Persisted when task is moved to "Done"
- Format: "Focus: 78%"

## Testing

Unit tests cover:
- Focus score calculation with various segment combinations
- Aggregation behavior (same context accumulates time)
- Pending segment lifecycle
- `HasRealScore` property under various conditions

Test base class provides:
- In-memory SQLite database
- Configured `AppDbContext`
- `IServiceScopeFactory` for the service under test

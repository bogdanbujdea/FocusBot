# Subfeature 2: Daily Analytics Panel (All Tracking)

## Goal

Deliver a user-visible daily dashboard panel that summarizes focus behavior for the current local day across all tracking (not just deep work sessions), including:
- total focused/unclear/distracted duration,
- total distraction count,
- top distracting apps.

This subfeature depends on distraction events from Subfeature 1 and introduces daily rollup UX.

## User Value

Users can answer:
- "How focused was I today overall?"
- "How many times did I get distracted today?"
- "Which apps are hurting my focus most today?"

This supports same-day correction rather than only end-of-session reflection.

## User Experience

## Main Board Panel
Add a new panel on `KanbanBoardPage` (or equivalent dashboard surface):
- title: `Today's Focus Analytics`
- date context: local date (for example `Fri, Mar 13`)
- metrics:
  - Focused time
  - Unclear time
  - Distracted time
  - Distractions count
- section: `Top distracting apps` (top 3-5)

## Update Behavior
- panel updates live or near-live as tracking data changes,
- values should remain stable and not flicker when order ties occur.

## Empty State
- if no tracking today: show `No analytics for today yet. Start a task to begin tracking.`

## Behavioral Rules

### Day Definition
- Use local calendar day (`DateOnly` in local timezone), not UTC day.
- All aggregation and panel queries must respect local-day boundary.

### Data Source Scope
- Include all tracking for current local day:
  - session-linked and non-session tracking.
- Do not restrict to deep work sessions.

### Ranking
- Top app ranking order:
  1. distracted duration descending,
  2. distraction events count descending,
  3. app name ascending.

## Data and Aggregation Design

## Daily Rollup Model
Introduce/add daily aggregate storage:
- `AnalyticsDateLocal` (`DateOnly`)
- `FocusedSeconds`
- `UnclearSeconds`
- `DistractedSeconds`
- `DistractionCount`
- app-level aggregates (table or normalized aggregate records)

Keep this additive to existing focus segments and distraction events.

## Rollup Update Strategy
- Incremental update path:
  - on tracking tick / state transition completion, increment proper daily counters,
  - on emitted distraction event, increment `DistractionCount` and app aggregate bucket.
- Query path:
  - panel reads from daily rollup model for fast render.

## Consistency
- Maintain deterministic updates for the same input sequence.
- If incremental updates fail or are delayed, a later reconciliation (Subfeature 3) can rebuild.

## Implementation Details

### ViewModel Contract
Add properties for panel binding:
- `TodayFocusedDurationText`
- `TodayUnclearDurationText`
- `TodayDistractedDurationText`
- `TodayDistractionsCount`
- `TodayTopDistractingApps` collection
- `HasTodayAnalyticsData` and `TodayAnalyticsEmptyMessage`

### UI and Design Rule Compliance
Respect `.cursor/rules/focusbot-design.mdc`:
- use existing theme resources and elevation tokens,
- do not encode meaning with color alone,
- keep keyboard and automation accessibility intact.

### Performance
- avoid heavy recomputation on every UI frame,
- push updates on meaningful events (tick, state change, event emit),
- query only current local day in default view.

## Files Likely Affected

- `src/FocusBot.App/Views/KanbanBoardPage.xaml` (panel UI)
- `src/FocusBot.App.ViewModels/KanbanBoardViewModel.cs` (panel properties, update triggers)
- `src/FocusBot.Infrastructure/Data/AppDbContext.cs` (daily rollup tables)
- `src/FocusBot.Infrastructure/Services` (daily analytics query/update service)
- `src/FocusBot.Infrastructure/Migrations` (schema updates)

## Unit and Integration Test Plan (Mandatory)

Follow `.cursor/rules/focusbot-tests.mdc` exactly (AAA, naming, deterministic).

### Aggregation Tests
- `BucketIntoCurrentLocalDate_WhenEventOccursToday`
- `SplitAcrossDates_WhenTrackingCrossesMidnightLocalTime`
- `IncrementDistractionCount_WhenValidEventEmitted`
- `AggregateAppDurations_WhenMultipleEventsForSameApp`
- `OrderTopAppsByDurationThenCount_WhenValuesTie`

### ViewModel/Panel Tests
- `ShowEmptyState_WhenNoTodayDataExists`
- `ShowMetrics_WhenTodayDataExists`
- `ExposeTopApps_WhenDailyAppAggregatesExist`
- `KeepStableOrder_WhenAppTotalsAreEqual`

### Suggested Test Locations
- `tests/FocusBot.Infrastructure.Tests/Services/DailyAnalyticsRollupServiceTests/*`
- `tests/FocusBot.Infrastructure.Tests/Data/*` (if repository-specific)
- `tests/FocusBot.App.ViewModels.Tests/KanbanBoardViewModelTests/*`

## Manual User Test Checklist

1. Start working on a task in morning.
2. Trigger focused and distracted periods across at least two apps.
3. Open main board and verify panel values are present and plausible.
4. Continue work and ensure panel updates without app restart.
5. Validate top apps ordering with repeated distractions.
6. End day boundary scenario:
   - run around local midnight,
   - verify new day starts fresh.

## Acceptance Criteria

- Main board displays a functioning `Today's Focus Analytics` panel.
- Panel uses all tracking data for local day.
- Distraction count and top apps are accurate and stable.
- Empty state and non-empty state render correctly.
- Unit/integration tests validate local-day bucketing, ranking, and panel behavior.

## Non-Goals for This Subfeature

- Top distracting websites.
- Startup/restart reconciliation logic.

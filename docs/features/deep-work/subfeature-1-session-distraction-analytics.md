# Subfeature 1: Session Distraction Analytics

## Goal

Deliver immediate session-level value by showing:
- distraction count (`Distractions: N`) in end-of-session summary,
- top distracting apps for the session,
- counting logic that is stable and predictable (one event per distracted episode after 5 seconds continuous distraction).

This subfeature must be fully usable on its own, even before daily analytics panel and website analytics are added.

## User Value

At the end of a deep work session, users can answer:
- "How many times did I break focus?"
- "Which app distracted me the most?"

This enables fast reflection and behavior correction in the next session.

## User Experience

### Entry
- User starts a deep work session on an In Progress task.
- Existing focus classification continues to run every window change.

### During Session
- No additional interruption in-session for this subfeature.
- Distractions are tracked silently in background.

### End of Session Summary (new fields)
- `Distractions: <count>`
- `Top distracting apps`
  - list top 3 by distracted duration,
  - tie-breaker by event count,
  - show app/process display name and duration.

### Empty States
- If no distractions: show `Distractions: 0` and `No distracting apps recorded`.

## Behavioral Rules

### Distraction Event Rule (authoritative)
Emit one `DistractionEvent` only when all conditions are true:
1. Focus status transitions into Distracted.
2. Distracted state remains continuous for at least 5 seconds.
3. Event for the current distracted episode has not yet been emitted.

Stop conditions:
- If status leaves Distracted before 5 seconds: emit nothing.
- If status remains Distracted after event emitted: do not emit additional events.
- A new event can only occur after status exits Distracted and later re-enters Distracted.

### Session Scoping
- Only events whose timestamp falls within `[SessionStartUtc, SessionEndUtc]` are counted in session summary.
- If session ends while currently in a not-yet-emitted distracted candidate (for example 3 seconds): do not emit.

## Data and Domain Design

## New/Extended Entities
- `DistractionEvent`
  - `Id` (GUID or DB key)
  - `OccurredAtUtc` (DateTime)
  - `TaskId` (string, nullable if needed for non-task contexts, but expected populated here)
  - `SessionId` (nullable in global model; populated in this subfeature when session active)
  - `ProcessName` (string)
  - `WindowTitleSnapshot` (string, optional but useful for troubleshooting)
  - `DistractedDurationSecondsAtEmit` (int; expected >= 5 for threshold emit)

## Service Responsibilities
- `IDistractionDetectorService` (Core interface; Infrastructure implementation)
  - consumes focus status transitions with timestamps,
  - handles 5-second candidate logic,
  - emits normalized `DistractionEvent` payloads.
- `IDistractionAnalyticsService`
  - query session events,
  - compute top apps for session,
  - return stable sorted result for UI.

## Implementation Details

### Integration Points
- Hook detector updates from the place where focus status changes are finalized (currently tied to `KanbanBoardViewModel` classification/update flow).
- Ensure detector receives monotonic timestamp source (prefer injectable clock in service for testability).

### Storage
- Persist distraction events through repository in Infrastructure.
- Keep schema additive; do not alter existing `FocusSegment` semantics.

### Sorting and Aggregation
- Top apps sort:
  1. distracted duration descending,
  2. event count descending,
  3. app name ascending (final deterministic tie-break).

### Performance
- Session summary query should be scoped by `SessionId` or `(TaskId + time window)`.
- Query target is small (single session window), expected fast.

## Files Likely Affected

- `src/FocusBot.Core/Entities` (new event/DTO types)
- `src/FocusBot.Core/Interfaces` (detector and analytics contracts)
- `src/FocusBot.Infrastructure/Data/AppDbContext.cs` (DB set + config)
- `src/FocusBot.Infrastructure/Services` (detector + analytics service implementation)
- `src/FocusBot.App.ViewModels/KanbanBoardViewModel.cs` (wire transition events)
- session summary viewmodel/view files (where summary metrics are displayed)
- `src/FocusBot.Infrastructure/Migrations` (new migration)

## Unit Test Plan (Mandatory)

Follow project test rules from `.cursor/rules/focusbot-tests.mdc`:
- one behavior per test,
- explicit AAA comments,
- naming `Result_WhenCondition`,
- deterministic time source (no real clock sleeps).

### Detector Tests
- `EmitSingleEvent_WhenDistractedStatePersistsForFiveSeconds`
- `DoNotEmit_WhenDistractedStateEndsBeforeFiveSeconds`
- `DoNotEmitRepeatedly_WhenStillInSameDistractedEpisode`
- `EmitSecondEvent_WhenStateReentersDistractedAfterExit`
- `ResetCandidate_WhenStateBecomesFocusedBeforeThreshold`

### Session Analytics Tests
- `ReturnZeroDistractions_WhenSessionHasNoEvents`
- `ReturnSessionDistractionCount_WhenSessionHasEventsInWindow`
- `IgnoreEventsOutsideSessionWindow_WhenCalculatingSessionCount`
- `ReturnTopAppsOrderedByDurationThenCount_WhenSummaryRequested`

### Suggested Test Locations
- `tests/FocusBot.Infrastructure.Tests/Services/DistractionDetectorServiceTests/*`
- `tests/FocusBot.Infrastructure.Tests/Services/SessionDistractionAnalyticsServiceTests/*`
- `tests/FocusBot.App.ViewModels.Tests/*` (binding and summary mapping only)

## Manual User Test Checklist

1. Start a session.
2. Trigger a distraction > 5s (for example open known distracting app for 8-10s), then return to focused app.
3. End session.
4. Verify:
   - `Distractions` increased by 1.
   - distracting app appears in top apps.
5. Repeat distraction quickly (< 5s).
6. End session and verify count does not increase for short spike.
7. Stay distracted for 20+ seconds in one continuous run and verify only one event counted for that episode.

## Acceptance Criteria

- Session summary displays distraction count and top distracting apps.
- Counting obeys 5-second continuous threshold and one-event-per-episode rule.
- Deterministic top-app ordering is stable.
- Unit tests cover detector edge cases and session analytics query behavior.
- No regressions in existing focus score/session summary behavior.

## Non-Goals for This Subfeature

- Daily analytics panel.
- Website/domain analytics.
- Restart/crash reconciliation.

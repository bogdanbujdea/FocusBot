# Phase 5: Focus Score

**User Feature**: See a focus score (0-100%) showing how well you stayed on task.

**End State**: Each task displays a focus score based on alignment scores weighted by time spent.

## What to Build

### Core Layer

- Add `FocusScorePercent` (0-100) to `UserTask`

### Infrastructure Layer

- Focus score calculation logic in orchestrator or dedicated service

### App Layer

- Live focus score display on InProgress task card (updates every second)
- Focus score shown on completed tasks in Done column
- Task history view with date navigation showing:
  - Tasks worked on that day
  - Time logged per task
  - Focus score per task
  - Daily summary

## Focus Score Calculation

```
focusScorePercent = (sum of alignmentScore * secondsAtThatScore) / totalSeconds * 10
```

- Time-weighted average of alignment scores (1-10 scale, converted to 0-100%)
- Updates live as user works
- Persisted when task moves to Done or ToDo

## UI Mockup

```
┌─────────────────────────────────────────────────────────┐
│   In Progress                    │   Done              │
│   ┌─────────────────────────┐    │   ┌─────────────┐   │
│   │ Write documentation     │    │   │ Code review │   │
│   │ ⏱ 00:45:23              │    │   │ 1h 23m      │   │
│   │ Focus: 78%              │    │   │ Focus: 92%  │   │
│   └─────────────────────────┘    │   └─────────────┘   │
└─────────────────────────────────────────────────────────┘
```

## Tests

- Focus score calculation tests
- Score persistence tests

## Checklist

- [ ] Add `FocusScorePercent` property to `UserTask`
- [ ] Implement focus score calculation logic
- [ ] Track score-time pairs during task session
- [ ] Update focus score live every second
- [ ] Add focus score display to InProgress task card
- [ ] Add focus score display to Done task cards
- [ ] Persist focus score when task moves to Done/ToDo
- [ ] Create Task History page
- [ ] Add date navigation (previous/next day)
- [ ] Display tasks with time and focus score per day
- [ ] Add daily summary
- [ ] Write focus score calculation tests
- [ ] Write score persistence tests

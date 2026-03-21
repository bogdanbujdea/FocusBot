# Sessions Slice

## Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/sessions` | Required | Start a new focus session |
| POST | `/sessions/{id}/end` | Required | End an active session with summary data |
| POST | `/sessions/{id}/pause` | Required | Pause an active running session |
| POST | `/sessions/{id}/resume` | Required | Resume a paused session |
| GET | `/sessions/active` | Required | Get the current active session |
| GET | `/sessions` | Required | Get paginated completed session history |
| GET | `/sessions/{id}` | Required | Get a single session by ID |

## `POST /sessions`

Starts a new focus session for the authenticated user. Returns 409 if an active (un-ended) session already exists.

**Request body:**
```json
{
  "sessionTitle": "Write unit tests for SessionService",
  "sessionContext": "Focus on edge cases"
}
```

**Response 201:**
```json
{
  "id": "uuid",
  "sessionTitle": "Write unit tests for SessionService",
  "sessionContext": "Focus on edge cases",
  "deviceId": null,
  "startedAtUtc": "2025-01-15T10:00:00Z",
  "endedAtUtc": null,
  "pausedAtUtc": null,
  "totalPausedSeconds": 0,
  "isPaused": false,
  "focusScorePercent": null,
  "focusedSeconds": null,
  "distractedSeconds": null,
  "distractionCount": null,
  "contextSwitchCount": null,
  "topDistractingApps": null,
  "topAlignedApps": null,
  "source": "api"
}
```

**Response 409:** An active session already exists.

## `POST /sessions/{id}/end`

Ends an active session and records focus summary data.

**Request body:**
```json
{
  "focusScorePercent": 85,
  "focusedSeconds": 2400,
  "distractedSeconds": 300,
  "distractionCount": 5,
  "contextSwitchCostSeconds": 120,
  "topDistractingApps": "[\"Slack\",\"Twitter\"]"
}
```

**Response 200:** Updated session with summary fields populated.
POST /sessions/{id}/pause`

Pauses an active running session. Time tracking should be stopped on the client until the session is resumed. 
The pause duration is automatically accumulated when the session is resumed or ended.

**Request body:** None (empty object).

**Response 200:** Updated session with `pausedAtUtc` set and `isPaused` = true.

**Response 404:** Session not found or does not belong to user.

**Response 409:** Session is already paused or already ended.

## `POST /sessions/{id}/resume`

Resumes a paused session. The pause duration is calculated and added to `totalPausedSeconds`.

**Request body:** None (empty object).

**Response 200:** Updated session with `pausedAtUtc` cleared, `isPaused` = false, and `totalPausedSeconds` incremented.

**Response 404:** Session not found or does not belong to user.

**Response 409:** Session is not paused or already ended.

## `
**Response 404:** Session not found or does not belong to user.

**Response 409:** Session is already ended.

## `GET /sessions/active`

Returns the user's currently active (un-ended) session.

**Response 200:** Session object.

**Response 404:** No active session.

## `GET /sessions`

Returns paginated completed sessions for the authenticated user, ordered by most recent first.
### Session States

Sessions have three states:

1. **Active + Running**: `endedAtUtc = null`, `pausedAtUtc = null`, `isPaused = false`
2. **Active + Paused**: `endedAtUtc = null`, `pausedAtUtc != null`, `isPaused = true`
3. **Ended**: `endedAtUtc != null`, `isPaused = false` (pause state is cleared on end)

### State Transitions

- **Start → Running**: New session created with `pausedAtUtc = null`
- **Running → Paused**: `PauseSessionAsync` sets `pausedAtUtc = DateTime.UtcNow`
- **Paused → Running**: `ResumeSessionAsync` adds pause duration to `totalPausedSeconds` and clears `pausedAtUtc`
- **Paused → Ended**: `EndSessionAsync` automatically resumes (accumulates final pause duration) before ending
- **Running → Ended**: `EndSessionAsync` sets `endedAtUtc = DateTime.UtcNow`

### Duration Calculation

- **Clock duration**: `endedAtUtc - startedAtUtc`
- **Effective active duration**: `(endedAtUtc - startedAtUtc) - totalPausedSeconds`
- The `totalPausedSeconds` field accumulates pause time across multiple pause/resume cycles

### Constraints

- Only one active session per user is enforced via a unique filtered index (`EndedAtUtc IS NULL`).
- `StartSessionAsync` checks for an active session before inserting and returns 409 on conflict.
- `EndSessionAsync` marks the session ended and stores the focus metrics provided by the client.
- Pausing a paused session or resuming a running session returns 409
- `pageSize` (int, default 20)

**Response 200:**
```json
{
  "items": [ /* SessionResponse[] */ ],
  "totalCount": 42,
  "page": 1,
  "pageSize": 20
}
```

## `GET /sessions/{id}`

Returns a single session by ID, scoped to the authenticated user.

**Response 200:** Session object.

**Response 404:** Session not found or does not belong to user.

## Business Rules

- Only one active session per user is enforced via a unique filtered index (`EndedAtUtc IS NULL`).
- `StartSessionAsync` checks for an active session before inserting and returns 409 on conflict.
- `EndSessionAsync` marks the session ended and stores the focus metrics provided by the client.
- All session data is scoped to the authenticated user via JWT `sub` claim.

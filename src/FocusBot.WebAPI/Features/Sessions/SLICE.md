# Sessions Slice

## Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/sessions` | Required | Start a new focus session |
| POST | `/sessions/{id}/end` | Required | End an active session with summary data |
| GET | `/sessions/active` | Required | Get the current active session |
| GET | `/sessions` | Required | Get paginated completed session history |
| GET | `/sessions/{id}` | Required | Get a single session by ID |

## `POST /sessions`

Starts a new focus session for the authenticated user. Returns 409 if an active (un-ended) session already exists.

**Request body:**
```json
{
  "taskText": "Write unit tests for SessionService",
  "taskHints": "Focus on edge cases"
}
```

**Response 201:**
```json
{
  "id": "uuid",
  "taskText": "Write unit tests for SessionService",
  "taskHints": "Focus on edge cases",
  "startedAtUtc": "2025-01-15T10:00:00Z",
  "endedAtUtc": null,
  "focusScorePercent": null,
  "focusedSeconds": null,
  "distractedSeconds": null,
  "distractionCount": null,
  "contextSwitchCostSeconds": null,
  "topDistractingApps": null,
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

**Response 404:** Session not found or does not belong to user.

**Response 409:** Session is already ended.

## `GET /sessions/active`

Returns the user's currently active (un-ended) session.

**Response 200:** Session object.

**Response 404:** No active session.

## `GET /sessions`

Returns paginated completed sessions for the authenticated user, ordered by most recent first.

**Query parameters:**
- `page` (int, default 1)
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

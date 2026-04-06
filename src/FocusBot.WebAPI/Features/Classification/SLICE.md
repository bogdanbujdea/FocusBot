# Classification Slice

## Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/classify` | Required | Classify focus alignment via LLM |

## `POST /classify`

Classifies whether the user's current window/tab aligns with their active task. Supports both BYOK (bring-your-own-key) and managed API key modes.

**Headers:**

| Header | Required | Description |
|--------|----------|-------------|
| `Authorization` | Yes | `Bearer <JWT>` |
| `X-Api-Key` | No | LLM provider API key for BYOK mode |

**Request body:**
```json
{
  "sessionTitle": "Writing quarterly report",
  "sessionContext": "Google Docs, spreadsheets",
  "processName": "msedge",
  "windowTitle": "Q3 Report - Google Docs",
  "url": "https://docs.google.com/...",
  "pageTitle": "Q3 Report",
  "providerId": "OpenAi",
  "modelId": "gpt-4o-mini"
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `sessionTitle` | Yes | What the user is trying to accomplish |
| `sessionContext` | No | Additional context about relevant apps/websites |
| `processName` | No | Windows executable name (e.g. "msedge") |
| `windowTitle` | No | Title bar text of the active window |
| `url` | No | Current browser URL (from extension) |
| `pageTitle` | No | Current page title (from extension) |
| `providerId` | No | LLM provider: "OpenAi", "Anthropic", "Google" (default: "OpenAi") |
| `modelId` | No | LLM model ID (default: "gpt-4o-mini") |

**Response 200:**
```json
{
  "score": 9,
  "reason": "Google Docs with quarterly report matches task",
  "cached": false
}
```

**Response 401:** Missing or invalid JWT.
**Response 422:** No API key available or LLM error.

## Caching

Results are cached in-process via `IMemoryCache` keyed by `(userId, contextHash, taskContentHash)` for 24 hours. The `cached` field in the response indicates whether the result was served from cache. Cache is process-local and does not survive restarts.

- `contextHash` = SHA-256 of `processName|windowTitle|url|pageTitle`
- `taskContentHash` = SHA-256 of `sessionTitle|sessionContext`
- Cache key format: `clf:{userId}:{contextHash}:{taskContentHash}`

## API Key Resolution

1. If `X-Api-Key` header is present → BYOK mode (uses provided key with optional `providerId`/`modelId`)
2. Otherwise → managed mode (uses `ManagedOpenAiKey` from configuration, always OpenAI gpt-4o-mini)

## Wiring (not yet registered)

`ClassificationService` and `ClassificationEndpoints.MapClassificationEndpoints()` will be registered in `Program.cs` after the sessions feature is merged. The `ClassificationCache` entity will be added to `ApiDbContext` at the same time.

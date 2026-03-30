# Classification Request Coalescing

## Why this exists

When both the Windows app and browser extension are running, they can emit near-simultaneous `POST /classify` requests for the same user. Without coordination, that can trigger duplicate LLM calls for the same moment of activity.

Coalescing reduces duplicate provider calls and keeps clients converged on one classification result.

## Behavior

- Scope: per user (`userId`)
- Window: 1 second
- Input: all `POST /classify` requests received during the window
- Output: one classification provider call and one shared `ClassifyResponse` fan-out to all waiting requests

## Priority rules

When selecting which request to classify from the current batch:

1. Non-browser desktop activity wins (`processName` is present and not a browser process)
2. Extension browser activity wins when context is browser (`url` is present)
3. Desktop browser request is fallback

This handles transitions like browser -> Docker Desktop correctly: non-browser desktop activity takes precedence.

## Latency impact

Each request may wait up to 1 second before classification starts. This is expected and enables deduplication.

## Client impact

- Clients continue calling `POST /classify` as before.
- Responses keep the same shape: `{ score, reason, cached }`.
- Multiple callers in the same coalescing window can receive the same response payload.
- After each successful classification (including the single call selected for a batch), the API broadcasts `ClassificationChanged` on the SignalR focus hub (`/hubs/focus`) to every connection in the user group. Payload includes `score`, `reason`, `source` (`extension` when the winning request had a URL, otherwise `desktop`), `activityName` (URL or process/window context), `classifiedAtUtc`, and `cached`. Connected Windows app and browser extension clients mirror this state so UI stays aligned when only one client triggered `POST /classify`.

## Error handling

If the selected classification call fails, all pending callers in that window receive the same error.

# Phase 6: Documentation + Build Verification

**Type:** Task
**Priority:** Medium
**Blocked by:** Phase 1, 2, 3, 4, 5
**Blocks:** None

## Summary

Update project documentation to reflect all changes from Phases 1-5 (status enum, idempotency, security fix, trial flow, race condition handling). Build all projects and run all test suites to verify nothing is broken.

## Acceptance Criteria

### Documentation
- [ ] `docs/paddle-implementation-summary.md` updated with:
  - `SubscriptionStatus` enum (replaces magic strings)
  - Idempotency via `ProcessedWebhookEvent`
  - Webhook verifier now rejects when secret is unconfigured
  - `past_due` maps to `Expired` (not `Active`)
  - Race condition fix for `transaction.completed`
  - Trial accepts a `PlanType` parameter
  - Plan type resolution failure behavior (log + skip, no default)
  - Updated status mapping table
- [ ] `AGENTS.md` updated with:
  - Note that Paddle trial should be removed from Dashboard prices
  - `SubscriptionStatus` enum values listed
  - Updated `ActivateTrial` endpoint signature (accepts `PlanType` in body)

### Build verification
- [ ] `dotnet build src/FocusBot.WebAPI/FocusBot.WebAPI.csproj` succeeds with no warnings
- [ ] `dotnet build tests/FocusBot.WebAPI.Tests/FocusBot.WebAPI.Tests.csproj` succeeds
- [ ] `dotnet build tests/FocusBot.WebAPI.IntegrationTests/FocusBot.WebAPI.IntegrationTests.csproj` succeeds

### Test verification
- [ ] `dotnet test tests/FocusBot.WebAPI.Tests` -- all tests pass
- [ ] `dotnet test tests/FocusBot.WebAPI.IntegrationTests` -- all tests pass
- [ ] `dotnet test tests/FocusBot.App.ViewModels.Tests` -- all tests pass (may need `SubscriptionStatus` change in `IFocusHubClient` or view model)
- [ ] `cd src/foqus-web-app && npm test` -- all tests pass
- [ ] No regressions in any test suite

## Technical Details

### paddle-implementation-summary.md changes

Update the **Status mapping** section:

```
Paddle           -> App (SubscriptionStatus enum)
"trialing"       -> Trial
"active"         -> Active
"past_due"       -> Expired (with warning log)
"paused"         -> Expired
"canceled"       -> Canceled
```

Add a **Webhook idempotency** section:

```
Every webhook event is checked against the ProcessedWebhookEvent table by event_id.
If already processed, the handler returns early. After successful processing, the
event_id is recorded. This prevents duplicate subscription creation on Paddle retries.
```

Update the **Webhook processing** section to mention:
- Typed deserialization (no JsonElement in service layer)
- Plan type resolution failure behavior
- transaction.completed race condition handling

### AGENTS.md Paddle section update

Add under the existing Paddle section:

```markdown
- **Trial activation**: `POST /subscriptions/trial` accepts `{ "planType": 1 }` (CloudBYOK) or `{ "planType": 2 }` (CloudManaged). Remove the 1-day trial from Paddle Dashboard prices; the server manages a 24h no-CC trial.
- **Subscription status**: Uses `SubscriptionStatus` enum (`None`, `Trial`, `Active`, `Expired`, `Canceled`). Serialized as camelCase strings in JSON responses.
- **Webhook idempotency**: Events are deduplicated by `event_id` via the `ProcessedWebhookEvent` table.
- **Security**: `PaddleWebhookVerifier` rejects all requests when `WebhookSecret` is not configured (no bypass in dev).
```

### Potential downstream breakage

Check these files for `"active"`, `"trial"`, or `"none"` string comparisons that may need updating:

- `src/FocusBot.Core/Entities/ApiModels.cs` -- `ApiSubscriptionStatus` record
- `src/FocusBot.App.ViewModels/FocusPageViewModel.cs` -- any status checks
- `src/FocusBot.Infrastructure/Services/FocusHubClientService.cs` -- SignalR handler
- `tests/FocusBot.App.ViewModels.Tests/` -- any status assertions

If `ApiSubscriptionStatus` in Core uses string status, it may need to be updated to use the enum (or stay as string if the desktop app consumes the API response as-is via HTTP).

## Files to Modify

| File | Change |
|------|--------|
| `docs/paddle-implementation-summary.md` | Update status mapping, add idempotency and security sections |
| `AGENTS.md` | Add trial, enum, idempotency, and security notes |

## Notes

- If the ViewModel tests or Core project reference `SubscriptionStatus`, those projects will also need the enum. Since `SubscriptionStatus` lives in `FocusBot.WebAPI`, and the desktop app communicates via HTTP (not shared types), the desktop side likely uses its own string/enum for status. Verify this before assuming downstream changes are needed.
- Run integration tests last since they spin up the full WebApplicationFactory and will catch configuration issues.

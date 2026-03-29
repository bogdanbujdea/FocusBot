# Phase 3: Service Logic Fixes

**Type:** Task
**Priority:** High
**Blocked by:** Phase 1, Phase 2
**Blocks:** Phase 4, 5

## Summary

Refactor `SubscriptionService` to add idempotent webhook processing, fix correctness issues (CanceledAt, past_due mapping, plan type resolution), handle the transaction.completed race condition, update trial activation to accept a plan type, and add structured event logging. All methods should be small with a single clear purpose.

## Acceptance Criteria

### Idempotency
- [ ] Each webhook handler checks `ProcessedWebhookEvent` by `eventId` at the start
- [ ] If the event was already processed, log at `Debug` level and return early
- [ ] After successful processing, insert a `ProcessedWebhookEvent` record
- [ ] Idempotency check and record insertion happen within the same `SaveChangesAsync` call

### subscription.canceled -- CanceledAt
- [ ] `CancelledAtUtc` is set from `sub.CanceledAt` (the Paddle subscription's actual cancellation timestamp)
- [ ] Falls back to `occurredAt` only if `sub.CanceledAt` is null

### past_due status mapping
- [ ] `"past_due"` maps to `SubscriptionStatus.Expired` (not `Active`)
- [ ] A warning is logged: `"Subscription {PaddleSubscriptionId} is past_due; marking as Expired"`

### Plan type resolution -- no default
- [ ] `MapPlanType` returns `PlanType?` (nullable), not a default
- [ ] When plan type is `null` in `subscription.created`, log an error with all available context (eventId, subscriptionId, customData) and return without creating/updating the subscription
- [ ] The webhook returns 200 to Paddle (so it doesn't retry endlessly), but no subscription row is created
- [ ] The user's billing page shows their current state (free/trial), prompting them to try again

### Race condition -- transaction.completed
- [ ] If no subscription row exists for the `PaddleSubscriptionId`, resolve user from `txn.CustomData?.UserId`
- [ ] If user is resolved, create a subscription row with status `Active`, populate with transaction data (payment details, billing period)
- [ ] Log a warning: `"transaction.completed arrived before subscription.created for {PaddleSubscriptionId}; creating row from transaction data"`
- [ ] If user cannot be resolved either, log an error and return (no crash)

### Trial activation with PlanType
- [ ] `ActivateTrialAsync` signature: `(Guid userId, PlanType planType, CancellationToken ct)`
- [ ] The created subscription row has `PlanType` set to the passed value (not `FreeBYOK`)

### Event ID logging
- [ ] Every handler logs at `Information` level at the start: `"Processing Paddle {EventType} event {EventId}"`
- [ ] After successful DB save: `"Completed Paddle event {EventId}"`

### Code quality
- [ ] Extract small, single-purpose private methods (see suggestions below)
- [ ] No method longer than ~25 lines
- [ ] Clear naming that describes what the method does

## Technical Details

### Suggested method decomposition

```
HandleSubscriptionCreatedAsync(sub, eventId, occurredAt, ct)
  |-- CheckIdempotency(eventId, ct)          --> returns bool (true = already processed)
  |-- ResolveUserId(sub.CustomData)           --> returns Guid?
  |-- ResolvePlanType(sub.CustomData, sub.Items) --> returns PlanType? (throws/logs if null)
  |-- MapSubscriptionStatus(paddleStatus)     --> returns SubscriptionStatus
  |-- FindOrCreateSubscription(userId, ct)    --> returns Subscription
  |-- ApplyPaddleSubscriptionFields(subscription, sub)  --> sets IDs, status, plan, etc.
  |-- ApplyBillingEnrichment(subscription, sub)          --> sets price, currency, period, etc.
  |-- RecordProcessedEvent(eventId, eventType, ct)
  |-- NotifyPlanChangedAsync(userId, ct)
```

The same helpers are reused across all 4 handlers, keeping each handler method as a short orchestration sequence.

### MapSubscriptionStatus update

```csharp
private SubscriptionStatus MapSubscriptionStatus(string? paddleStatus, string? paddleSubId = null)
{
    return paddleStatus switch
    {
        "active" => SubscriptionStatus.Active,
        "trialing" => SubscriptionStatus.Trial,
        "past_due" => LogAndReturn(SubscriptionStatus.Expired, paddleSubId),
        "paused" => SubscriptionStatus.Expired,
        "canceled" => SubscriptionStatus.Canceled,
        _ => SubscriptionStatus.None,
    };
}
```

### Race condition handler for transaction.completed

```csharp
public async Task HandleTransactionCompletedAsync(PaddleTransaction txn, string eventId, CancellationToken ct)
{
    if (await CheckIdempotency(eventId, ct)) return;

    var subscription = await db.Subscriptions
        .FirstOrDefaultAsync(s => s.PaddleSubscriptionId == txn.SubscriptionId, ct);

    if (subscription is null && txn.SubscriptionId is not null)
    {
        // Race condition: transaction arrived before subscription.created
        var userId = ResolveUserId(txn.CustomData);
        if (userId is null)
        {
            logger.LogError("transaction.completed {EventId}: no subscription row and no user_id in custom_data for sub {SubId}",
                eventId, txn.SubscriptionId);
            return;
        }

        logger.LogWarning("transaction.completed arrived before subscription.created for {SubId}; creating row",
            txn.SubscriptionId);

        subscription = new Subscription
        {
            UserId = userId.Value,
            PaddleSubscriptionId = txn.SubscriptionId,
            PaddleCustomerId = txn.CustomerId,
            Status = SubscriptionStatus.Active,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        db.Subscriptions.Add(subscription);
    }

    if (subscription is null) return;

    ApplyTransactionEnrichment(subscription, txn);
    await RecordAndSaveAsync(eventId, "transaction.completed", ct);
    await NotifyPlanChangedAsync(subscription.UserId, ct);
}
```

## Files to Modify

| File | Change |
|------|--------|
| `src/FocusBot.WebAPI/Features/Subscriptions/SubscriptionService.cs` | All changes in this phase |

## Notes

- After this phase, the service layer is complete and correct. Phases 4 and 5 adapt the surrounding layers (endpoints, DTOs, frontend, tests).
- The `CheckIdempotency` + `RecordProcessedEvent` pattern means every webhook event is recorded. This doubles as an audit trail for support debugging.
- For the plan type resolution failure: returning 200 to Paddle is important. If we returned 4xx/5xx, Paddle would retry the same malformed event indefinitely. The error should be caught through log monitoring/alerts.

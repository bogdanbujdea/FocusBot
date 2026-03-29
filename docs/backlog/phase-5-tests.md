# Phase 5: Tests

**Type:** Task
**Priority:** High
**Blocked by:** Phase 1, 2, 3, 4
**Blocks:** Phase 6

## Summary

Add unit tests for `subscription.created` and `subscription.canceled` webhook handlers (critical payment paths), plus tests for the verifier rejection, past_due mapping, and trial with plan type. Update existing tests for the `SubscriptionStatus` enum.

## Acceptance Criteria

### subscription.created tests
- [ ] Creates row with correct `SubscriptionStatus.Active`, plan type, and enrichment fields (price, currency, billing interval, period dates)
- [ ] Idempotent: calling with the same `eventId` twice results in only one subscription row
- [ ] Upgrades existing trial: when a user with status `Trial` gets a `subscription.created`, the row is updated to `Active` with Paddle IDs
- [ ] Plan type resolution failure: when `custom_data` has no valid `plan_type`, the handler logs an error and does NOT create a subscription row

### subscription.canceled tests
- [ ] Sets status to `SubscriptionStatus.Canceled`
- [ ] `CancelledAtUtc` is populated from `sub.CanceledAt` (not `OccurredAt`)
- [ ] `CancellationReason` is populated from `sub.ScheduledChange.Action`
- [ ] Idempotent: calling with the same `eventId` twice does not change the row a second time

### past_due status mapping
- [ ] `MapSubscriptionStatus("past_due")` returns `SubscriptionStatus.Expired`

### Webhook verifier
- [ ] `TryVerify` returns `false` when `webhookSecret` is empty string
- [ ] `TryVerify` returns `false` when `webhookSecret` is null

### Trial activation
- [ ] `ActivateTrialAsync(userId, PlanType.CloudBYOK)` creates a subscription with `PlanType.CloudBYOK` and status `Trial`
- [ ] `ActivateTrialAsync(userId, PlanType.CloudManaged)` creates a subscription with `PlanType.CloudManaged`

### Existing test updates
- [ ] All existing tests use `SubscriptionStatus` enum instead of string comparisons
- [ ] All existing tests calling `HandlePaddleWebhookAsync` are updated to call the new typed methods directly (passing `PaddleSubscription` objects, not JSON strings)
- [ ] `ActivateTrialAsync` calls pass a `PlanType` parameter

## Technical Details

### Test structure

Tests should call the typed service methods directly (from Phase 2/3), passing constructed `PaddleSubscription` / `PaddleTransaction` objects. This is cleaner than building JSON strings and removes the JSON parsing layer from test scope.

Example:

```csharp
[Fact]
public async Task HandleSubscriptionCreatedAsync_CreatesRowWithCorrectStatus()
{
    await using var db = CreateInMemoryDb();
    var service = CreateService(db);
    var userId = Guid.NewGuid();

    var sub = new PaddleSubscription
    {
        Id = "sub_test_1",
        Status = "active",
        CustomerId = "ctm_test",
        CustomData = new PaddleCustomData
        {
            UserId = userId.ToString(),
            PlanType = "cloud-managed",
        },
        Items =
        [
            new PaddleSubscriptionItem
            {
                Price = new PaddlePrice
                {
                    Id = "pri_1",
                    ProductId = "pro_1",
                    UnitPrice = new PaddleUnitPrice { Amount = "499", CurrencyCode = "USD" },
                    BillingCycle = new PaddleBillingCycle { Interval = "month", Frequency = 1 },
                },
            },
        ],
        CurrentBillingPeriod = new PaddleBillingPeriod
        {
            EndsAt = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        },
        NextBilledAt = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    await service.HandleSubscriptionCreatedAsync(sub, "evt_test_1", DateTime.UtcNow, CancellationToken.None);

    var row = await db.Subscriptions.SingleAsync(s => s.UserId == userId);
    row.Status.Should().Be(SubscriptionStatus.Active);
    row.PlanType.Should().Be(PlanType.CloudManaged);
    row.PaddleSubscriptionId.Should().Be("sub_test_1");
    row.CurrencyCode.Should().Be("USD");
    row.UnitAmountMinor.Should().Be(499);
}
```

### Idempotency test pattern

```csharp
[Fact]
public async Task HandleSubscriptionCreatedAsync_IsIdempotent()
{
    await using var db = CreateInMemoryDb();
    var service = CreateService(db);
    var sub = BuildTestSubscription(Guid.NewGuid());

    await service.HandleSubscriptionCreatedAsync(sub, "evt_1", DateTime.UtcNow, CancellationToken.None);
    await service.HandleSubscriptionCreatedAsync(sub, "evt_1", DateTime.UtcNow, CancellationToken.None);

    (await db.Subscriptions.CountAsync()).Should().Be(1);
    (await db.Set<ProcessedWebhookEvent>().CountAsync()).Should().Be(1);
}
```

### Plan type resolution failure test

```csharp
[Fact]
public async Task HandleSubscriptionCreatedAsync_LogsErrorAndSkips_WhenPlanTypeUnresolvable()
{
    await using var db = CreateInMemoryDb();
    var service = CreateService(db);

    var sub = new PaddleSubscription
    {
        Id = "sub_no_plan",
        Status = "active",
        CustomerId = "ctm_test",
        CustomData = new PaddleCustomData { UserId = Guid.NewGuid().ToString() },
        // No plan_type in custom_data, no items with price custom_data
    };

    await service.HandleSubscriptionCreatedAsync(sub, "evt_no_plan", DateTime.UtcNow, CancellationToken.None);

    (await db.Subscriptions.CountAsync()).Should().Be(0);
}
```

## Files to Modify

| File | Change |
|------|--------|
| `tests/FocusBot.WebAPI.Tests/Features/Subscriptions/SubscriptionServiceTests.cs` | Add new tests, update existing tests for enum + typed methods |
| `tests/FocusBot.WebAPI.Tests/Features/Subscriptions/PaddleWebhookVerifierTests.cs` | Update empty-secret test, add null-secret test |

## Notes

- The `CreateService` helper may need updating to include a `DbSet<ProcessedWebhookEvent>` in the in-memory DB (it should work automatically since the same `ApiDbContext` is used).
- Consider adding a `BuildTestSubscription(Guid userId)` helper method to reduce boilerplate across tests.
- We are intentionally NOT testing `subscription.updated` or `transaction.completed` in this phase. If the user wants coverage for those later, they can be added as a follow-up.

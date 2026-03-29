# Phase 2: Security + Deserialization

**Type:** Task
**Priority:** High
**Blocked by:** Phase 1
**Blocks:** Phase 3, 4, 5

## Summary

Fix the webhook verifier to reject requests when the webhook secret is not configured (instead of silently accepting them). Refactor deserialization so that `SubscriptionService` receives typed C# objects -- no `JsonElement` usage in the service layer.

## Acceptance Criteria

- [ ] `PaddleWebhookVerifier.TryVerify` returns `false` when `webhookSecret` is null or empty
- [ ] Error message is `"Webhook secret is not configured; rejecting request."`
- [ ] Existing verifier tests updated to match new behavior
- [ ] `PaddleWebhookPayload.Data` type changed from `object?` to `JsonElement`
- [ ] `SubscriptionEndpoints.HandlePaddleWebhook` deserializes the raw body into `PaddleWebhookPayload`, examines `EventType`, and deserializes `Data` into the appropriate typed class (`PaddleSubscription` or `PaddleTransaction`)
- [ ] `SubscriptionService` no longer imports or uses `System.Text.Json.JsonElement` or `JsonDocument`
- [ ] Service exposes typed public methods per event type (see below)
- [ ] Project builds with no warnings

## Technical Details

### Webhook verifier fix

In `PaddleWebhookVerifier.TryVerify`, change the empty-secret branch:

```csharp
// BEFORE (insecure)
if (string.IsNullOrEmpty(webhookSecret))
{
    error = "Webhook secret is not configured; signature verification skipped.";
    return true;
}

// AFTER
if (string.IsNullOrEmpty(webhookSecret))
{
    error = "Webhook secret is not configured; rejecting request.";
    return false;
}
```

Update the `TryVerify_ReturnsTrue_WhenSecretEmpty` test in `PaddleWebhookVerifierTests.cs` to expect `false`.

### Deserialization refactor

**Step 1 -- PaddleWebhookModels.cs:** Change `Data` from `object?` to `JsonElement`:

```csharp
[JsonPropertyName("data")]
public JsonElement Data { get; set; }
```

`JsonElement` is only used here as a deserialization stepping stone. The service layer never touches it.

**Step 2 -- SubscriptionEndpoints.cs:** Replace the current `HandlePaddleWebhook` implementation. After signature verification, deserialize `rawBody` to `PaddleWebhookPayload`, then route:

```csharp
var envelope = JsonSerializer.Deserialize<PaddleWebhookPayload>(rawBody);
if (envelope is null) return Results.Ok();

switch (envelope.EventType)
{
    case "subscription.created":
        var subCreated = envelope.Data.Deserialize<PaddleSubscription>();
        if (subCreated is not null)
            await service.HandleSubscriptionCreatedAsync(subCreated, envelope.EventId, envelope.OccurredAt, ct);
        break;

    case "subscription.updated":
        var subUpdated = envelope.Data.Deserialize<PaddleSubscription>();
        if (subUpdated is not null)
            await service.HandleSubscriptionUpdatedAsync(subUpdated, envelope.EventId, ct);
        break;

    case "subscription.canceled":
        var subCanceled = envelope.Data.Deserialize<PaddleSubscription>();
        if (subCanceled is not null)
            await service.HandleSubscriptionCanceledAsync(subCanceled, envelope.EventId, ct);
        break;

    case "transaction.completed":
        var txn = envelope.Data.Deserialize<PaddleTransaction>();
        if (txn is not null)
            await service.HandleTransactionCompletedAsync(txn, envelope.EventId, ct);
        break;
}

return Results.Ok();
```

**Step 3 -- SubscriptionService.cs:** Remove `HandlePaddleWebhookAsync(JsonElement)` and the private `HandleSubscriptionEvent` / `HandleTransactionCompleted` methods. Replace with four public methods:

```csharp
public async Task HandleSubscriptionCreatedAsync(PaddleSubscription sub, string eventId, DateTime? occurredAt, CancellationToken ct)
public async Task HandleSubscriptionUpdatedAsync(PaddleSubscription sub, string eventId, CancellationToken ct)
public async Task HandleSubscriptionCanceledAsync(PaddleSubscription sub, string eventId, CancellationToken ct)
public async Task HandleTransactionCompletedAsync(PaddleTransaction txn, string eventId, CancellationToken ct)
```

Each method receives fully typed objects. No JSON parsing or `JsonElement` in the service.

## Files to Modify

| File | Change |
|------|--------|
| `src/FocusBot.WebAPI/Features/Subscriptions/PaddleWebhookVerifier.cs` | Deny when secret is empty |
| `src/FocusBot.WebAPI/Features/Subscriptions/PaddleWebhookModels.cs` | `Data` from `object?` to `JsonElement` |
| `src/FocusBot.WebAPI/Features/Subscriptions/SubscriptionEndpoints.cs` | Typed deserialization + dispatch by event type |
| `src/FocusBot.WebAPI/Features/Subscriptions/SubscriptionService.cs` | Replace generic handler with 4 typed public methods, remove all `JsonElement`/`JsonDocument`/`JsonSerializer` usage |
| `tests/FocusBot.WebAPI.Tests/Features/Subscriptions/PaddleWebhookVerifierTests.cs` | Update empty-secret test to expect `false` |

## Notes

- After this phase, the existing `SubscriptionServiceTests` that call `HandlePaddleWebhookAsync` will break. They should be updated to call the new typed methods directly (passing `PaddleSubscription` / `PaddleTransaction` objects instead of JSON strings). This makes the tests cleaner and removes JSON dependency from test code.
- `JsonElement` remains in `PaddleWebhookPayload` (model layer) and in the endpoint method (HTTP layer). This is acceptable -- the goal is to keep it out of the service/business logic.

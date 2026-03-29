# Phase 4: Endpoints + DTOs + Frontend

**Type:** Task
**Priority:** High
**Blocked by:** Phase 1, 2, 3
**Blocks:** Phase 5

## Summary

Update `SubscriptionEndpoints` to use the new typed dispatch from Phase 2, update DTOs to use `SubscriptionStatus` enum, add a trial request body with plan type, and adjust the web app frontend to work with the enum string serialization.

## Acceptance Criteria

### Endpoints
- [ ] `HandlePaddleWebhook` uses the typed dispatch pattern (Phase 2 design)
- [ ] `ActivateTrial` endpoint accepts a JSON body: `{ "planType": 1 }` (PlanType enum integer)
- [ ] `ActivateTrial` returns 400 if planType is not `CloudBYOK` or `CloudManaged` (cannot trial the free plan)

### DTOs
- [ ] `SubscriptionStatusResponse.Status` is `SubscriptionStatus` (not `string`)
- [ ] `ActivateTrialResponse.Status` is `SubscriptionStatus` (not `string`)
- [ ] New `ActivateTrialRequest` record: `{ PlanType PlanType }`
- [ ] No breaking changes to other existing DTO fields

### Frontend (BillingPage.tsx + types.ts)
- [ ] `SubscriptionStatusResponse.status` in `types.ts` updated to `string` (it already is, but document the expected values: `"none"`, `"trial"`, `"active"`, `"expired"`, `"canceled"`)
- [ ] `BillingPage.tsx` status comparisons work correctly with camelCase enum strings
- [ ] Trial activation call sends `{ planType: PlanType.CloudBYOK }` (or the selected plan) in the request body

## Technical Details

### ActivateTrial endpoint update

```csharp
group.MapPost("/trial", async (
    ActivateTrialRequest request,
    SubscriptionService service,
    HttpContext ctx,
    CancellationToken ct) =>
{
    if (request.PlanType is not (PlanType.CloudBYOK or PlanType.CloudManaged))
        return Results.BadRequest(new { error = "Trial is only available for paid plans." });

    var userId = GetUserId(ctx);
    var result = await service.ActivateTrialAsync(userId, request.PlanType, ct);

    return result is null
        ? Results.Conflict(new { error = "Trial already activated or subscription exists." })
        : Results.Ok(result);
})
```

### DTO updates

```csharp
// Dtos.cs
public sealed record SubscriptionStatusResponse(
    SubscriptionStatus Status,
    PlanType PlanType,
    DateTime? TrialEndsAt,
    DateTime? CurrentPeriodEndsAt,
    DateTime? NextBilledAtUtc = null);

public sealed record ActivateTrialResponse(SubscriptionStatus Status, DateTime TrialEndsAt);

public sealed record ActivateTrialRequest(PlanType PlanType);
```

### Frontend serialization compatibility

The `SubscriptionStatus` enum uses `CamelCaseEnumConverter<SubscriptionStatus>` (from Phase 1), so the API returns:

| C# Enum Value | JSON Value | Frontend Comparison |
|---|---|---|
| `None` | `"none"` | `status === "none"` |
| `Trial` | `"trial"` | `status === "trial"` |
| `Active` | `"active"` | `status === "active"` |
| `Expired` | `"expired"` | `status === "expired"` |
| `Canceled` | `"canceled"` | `status === "canceled"` |

The existing frontend comparisons in `BillingPage.tsx` (lines 107-109, 149, 154, 159, 169) already use these exact lowercase strings. No changes needed for status comparisons.

For the trial activation call, the frontend needs to send `planType` as an integer (matching the existing `PlanType` enum which serializes as a number):

```typescript
// api/client.ts -- update activateTrial call
await api.post("/subscriptions/trial", { planType: PlanType.CloudBYOK });
```

### GetStatusAsync update

Return `SubscriptionStatus.None` instead of the string `"none"`:

```csharp
if (subscription is null)
    return new SubscriptionStatusResponse(SubscriptionStatus.None, PlanType.FreeBYOK, null, null, null);
```

## Files to Modify

| File | Change |
|------|--------|
| `src/FocusBot.WebAPI/Features/Subscriptions/SubscriptionEndpoints.cs` | Typed webhook dispatch, trial request body validation |
| `src/FocusBot.WebAPI/Features/Subscriptions/Dtos.cs` | `SubscriptionStatus` enum in responses, `ActivateTrialRequest` |
| `src/foqus-web-app/src/pages/BillingPage.tsx` | Trial activation sends planType in body |
| `src/foqus-web-app/src/api/types.ts` | Document expected status string values (no structural change) |
| `src/foqus-web-app/src/api/client.ts` | Update `activateTrial` to send request body |

## Notes

- The `PlanType` enum continues to serialize as an integer (0, 1, 2). Only `SubscriptionStatus` gets the string converter. This is intentional -- `PlanType` is used as a numeric identifier throughout the existing codebase.
- Verify that `IsSubscribedOrTrialActiveAsync` in `SubscriptionService.cs` also uses the `SubscriptionStatus` enum instead of string comparisons.

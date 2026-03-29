# Phase 1: Schema Changes

**Type:** Task
**Priority:** High
**Blocked by:** None
**Blocks:** Phase 2, 3, 4, 5

## Summary

Add a `SubscriptionStatus` enum to replace magic status strings, and a `ProcessedWebhookEvent` entity for webhook idempotency tracking. No EF migration or DB index in this phase.

## Acceptance Criteria

- [ ] `SubscriptionStatus` enum exists with values: `None`, `Trial`, `Active`, `Expired`, `Canceled`
- [ ] `Subscription.Status` property type changed from `string` to `SubscriptionStatus`
- [ ] Enum serializes as camelCase string in JSON (e.g. `"active"`, `"trial"`) for backward compatibility with the web app frontend
- [ ] `ProcessedWebhookEvent` entity exists with `EventId` (PK), `EventType`, `ProcessedAtUtc`
- [ ] `DbSet<ProcessedWebhookEvent>` registered in `ApiDbContext`
- [ ] No EF migration generated (will be handled separately)
- [ ] Project builds with no warnings

## Technical Details

### SubscriptionStatus enum

Add to `Subscription.cs` (or a separate file alongside it):

```csharp
[JsonConverter(typeof(JsonStringEnumConverter<SubscriptionStatus>))]
public enum SubscriptionStatus
{
    None,
    Trial,
    Active,
    Expired,
    Canceled,
}
```

Using `JsonStringEnumConverter` with `JsonNamingPolicy.CamelCase` ensures the API returns `"active"` instead of `"Active"` or `2`. This matches what the frontend (`BillingPage.tsx`) currently expects.

**Important:** The default `System.Text.Json` behavior serializes enums as integers. Without the converter, the frontend would break. Two options:
1. Apply `[JsonConverter(typeof(JsonStringEnumConverter<SubscriptionStatus>))]` on the enum itself
2. Register `JsonStringEnumConverter` globally in `Program.cs` via `ConfigureHttpJsonOptions`

Prefer option 1 (scoped) since `PlanType` already serializes as an integer and we don't want to break that.

To get camelCase output (`"active"` not `"Active"`), the attribute needs a naming policy. Since attribute constructors don't accept `JsonNamingPolicy`, the cleanest approach is a small custom converter class:

```csharp
public sealed class CamelCaseEnumConverter<T> : JsonStringEnumConverter<T> where T : struct, Enum
{
    public CamelCaseEnumConverter() : base(JsonNamingPolicy.CamelCase) { }
}
```

Then: `[JsonConverter(typeof(CamelCaseEnumConverter<SubscriptionStatus>))]`

### ProcessedWebhookEvent entity

New file or add to existing entities folder:

```csharp
public class ProcessedWebhookEvent
{
    public string EventId { get; set; } = "";
    public string EventType { get; set; } = "";
    public DateTime ProcessedAtUtc { get; set; }
}
```

In `ApiDbContext.OnModelCreating`:

```csharp
modelBuilder.Entity<ProcessedWebhookEvent>(entity =>
{
    entity.HasKey(e => e.EventId);
    entity.Property(e => e.EventId).HasMaxLength(100);
    entity.Property(e => e.EventType).HasMaxLength(100);
});
```

### Subscription.Status conversion

In `ApiDbContext.OnModelCreating`, ensure the enum is stored as string in PostgreSQL:

```csharp
entity.Property(s => s.Status)
      .HasConversion<string>()
      .HasMaxLength(20);
```

## Files to Modify

| File | Change |
|------|--------|
| `src/FocusBot.WebAPI/Data/Entities/Subscription.cs` | Add `SubscriptionStatus` enum, change `Status` property type, add `CamelCaseEnumConverter<T>` |
| `src/FocusBot.WebAPI/Data/ApiDbContext.cs` | Add `DbSet<ProcessedWebhookEvent>`, configure entity, add string conversion for `SubscriptionStatus` |

## Notes

- The `ProcessedWebhookEvent` entity will also need a `DbSet` in the new `ProcessedWebhookEvent.cs` file (or inline in `Subscription.cs` if keeping entities together).
- Do NOT generate a migration. The migration will be a separate task after all schema changes are finalized.
- After this phase, the project will not compile until Phase 2-4 update all string status comparisons. That is expected -- phases 2-4 should be applied together.

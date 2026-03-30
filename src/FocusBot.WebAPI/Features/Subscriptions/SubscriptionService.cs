using System.Globalization;
using System.Text.Json;
using FocusBot.WebAPI.Data;
using FocusBot.WebAPI.Data.Entities;
using FocusBot.WebAPI.Features.Pricing;
using FocusBot.WebAPI.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using JsonException = System.Text.Json.JsonException;

namespace FocusBot.WebAPI.Features.Subscriptions;

/// <summary>
/// Business logic for subscription lifecycle, trial activation, and Paddle webhook handling.
/// </summary>
public class SubscriptionService(
    ApiDbContext db,
    IHubContext<FocusHub, IFocusHubClient> hub,
    IPaddleBillingApi paddleBilling,
    ILogger<SubscriptionService> logger
)
{
    private Task<bool> UserExistsAsync(Guid userId, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.Id == userId, ct);

    /// <summary>
    /// Returns the current subscription status for a user. A trial row is normally created
    /// at account provisioning time (GET /auth/me). If no row exists here, it means the user
    /// bypassed provisioning, so we create the trial defensively when the user row exists.
    /// </summary>
    /// <returns>Null when the user is not provisioned in the database (call GET /auth/me first).</returns>
    public async Task<SubscriptionStatusResponse?> GetStatusAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var subscription = await db
            .Subscriptions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (subscription is null)
        {
            if (!await UserExistsAsync(userId, ct))
            {
                logger.LogWarning(
                    "GetStatusAsync: user {UserId} has no Users row; refusing to create subscription.",
                    userId
                );
                return null;
            }

            logger.LogWarning(
                "No subscription row found for user {UserId} during GetStatusAsync — creating trial defensively. User may not have called /auth/me first.",
                userId
            );

            var trialEnd = DateTime.UtcNow.AddHours(24);
            var trial = new Subscription
            {
                UserId = userId,
                Status = SubscriptionStatus.Trial,
                PlanType = PlanType.TrialFullAccess,
                TrialEndsAtUtc = trialEnd,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
            };
            db.Subscriptions.Add(trial);
            await db.SaveChangesAsync(ct);

            return new SubscriptionStatusResponse(
                SubscriptionStatus.Trial,
                PlanType.TrialFullAccess,
                trialEnd,
                null,
                null
            );
        }

        return new SubscriptionStatusResponse(
            subscription.Status,
            subscription.PlanType,
            subscription.TrialEndsAtUtc,
            subscription.CurrentPeriodEndsAtUtc,
            subscription.NextBilledAtUtc
        );
    }

    /// <summary>
    /// Activates a 24-hour trial for a user with the specified plan type.
    /// </summary>
    public async Task<ActivateTrialOutcome> ActivateTrialAsync(
        Guid userId,
        PlanType planType,
        CancellationToken ct = default
    )
    {
        if (!await UserExistsAsync(userId, ct))
        {
            logger.LogWarning(
                "ActivateTrialAsync: user {UserId} has no Users row.",
                userId
            );
            return new ActivateTrialOutcome(ActivateTrialResultKind.UserNotProvisioned);
        }

        var existing = await db.Subscriptions.FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (existing is not null)
            return new ActivateTrialOutcome(ActivateTrialResultKind.AlreadyExists);

        var trialEnd = DateTime.UtcNow.AddHours(24);
        var subscription = new Subscription
        {
            UserId = userId,
            Status = SubscriptionStatus.Trial,
            PlanType = planType,
            TrialEndsAtUtc = trialEnd,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync(ct);

        return new ActivateTrialOutcome(
            ActivateTrialResultKind.Created,
            new ActivateTrialResponse(SubscriptionStatus.Trial, trialEnd)
        );
    }

    /// <summary>
    /// Checks whether the user has an active subscription or a trial that has not expired.
    /// </summary>
    public async Task<bool> IsSubscribedOrTrialActiveAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var subscription = await db
            .Subscriptions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (subscription is null)
            return false;

        return subscription.Status switch
        {
            SubscriptionStatus.Active => true,
            SubscriptionStatus.Trial => subscription.TrialEndsAtUtc > DateTime.UtcNow,
            _ => false,
        };
    }

    /// <summary>
    /// Opens a Paddle customer portal session for the authenticated user.
    /// </summary>
    public async Task<string?> CreateCustomerPortalUrlAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var subscription = await db
            .Subscriptions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (subscription?.PaddleCustomerId is null)
            return null;

        return await paddleBilling.CreateCustomerPortalSessionAsync(
            subscription.PaddleCustomerId,
            subscription.PaddleSubscriptionId,
            ct
        );
    }

    /// <summary>
    /// Processes a subscription.created webhook event.
    /// </summary>
    public async Task HandleSubscriptionCreatedAsync(
        PaddleSubscription sub,
        string eventId,
        DateTime? occurredAt,
        CancellationToken ct = default
    )
    {
        logger.LogInformation("Processing Paddle subscription.created event {EventId}", eventId);

        if (sub?.Id is null)
            return;

        if (await IsEventAlreadyProcessed(eventId, ct))
        {
            logger.LogDebug("Event {EventId} already processed; skipping", eventId);
            return;
        }

        if (!TryResolveUserId(sub.CustomData, out var userId))
        {
            logger.LogError(
                "subscription.created event {EventId}: Failed to resolve user_id from custom_data",
                eventId
            );
            await RecordProcessedEvent(eventId, "subscription.created", ct);
            return;
        }

        if (!await UserExistsAsync(userId, ct))
        {
            logger.LogError(
                "subscription.created event {EventId}: User {UserId} not provisioned in database",
                eventId,
                userId
            );
            await RecordProcessedEvent(eventId, "subscription.created", ct);
            return;
        }

        var mappedPlan = MapPlanType(sub.CustomData, sub.Items);
        if (!mappedPlan.HasValue)
        {
            logger.LogError(
                "subscription.created event {EventId}: Failed to resolve plan_type for subscription {SubId}",
                eventId,
                sub.Id
            );
            await RecordProcessedEvent(eventId, "subscription.created", ct);
            return;
        }

        var mappedStatus = MapSubscriptionStatus(sub.Status);

        var existing = await db.Subscriptions.FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (existing is not null)
        {
            existing.PaddleSubscriptionId = sub.Id;
            existing.PaddleCustomerId = sub.CustomerId;
            existing.Status = mappedStatus;
            existing.PlanType = mappedPlan.Value;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            ApplySubscriptionEnrichment(existing, sub);
        }
        else
        {
            var created = new Subscription
            {
                UserId = userId,
                PaddleSubscriptionId = sub.Id,
                PaddleCustomerId = sub.CustomerId,
                Status = mappedStatus,
                PlanType = mappedPlan.Value,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
            };
            ApplySubscriptionEnrichment(created, sub);
            db.Subscriptions.Add(created);
        }

        await db.SaveChangesAsync(ct);
        await RecordProcessedEvent(eventId, "subscription.created", ct);
        logger.LogInformation("Completed Paddle event {EventId}", eventId);
        await NotifyPlanChangedAsync(userId, ct);
    }

    /// <summary>
    /// Processes a subscription.updated webhook event.
    /// </summary>
    public async Task HandleSubscriptionUpdatedAsync(
        PaddleSubscription sub,
        string eventId,
        CancellationToken ct = default
    )
    {
        logger.LogInformation("Processing Paddle subscription.updated event {EventId}", eventId);

        if (sub?.Id is null)
            return;

        if (await IsEventAlreadyProcessed(eventId, ct))
        {
            logger.LogDebug("Event {EventId} already processed; skipping", eventId);
            return;
        }

        var subscription = await db.Subscriptions.FirstOrDefaultAsync(
            s => s.PaddleSubscriptionId == sub.Id,
            ct
        );

        if (subscription is null)
        {
            await RecordProcessedEvent(eventId, "subscription.updated", ct);
            return;
        }

        subscription.Status = MapSubscriptionStatus(sub.Status);

        var mapped = MapPlanType(sub.CustomData, sub.Items);
        if (mapped.HasValue)
            subscription.PlanType = mapped.Value;

        ApplySubscriptionEnrichment(subscription, sub);
        subscription.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await RecordProcessedEvent(eventId, "subscription.updated", ct);
        logger.LogInformation("Completed Paddle event {EventId}", eventId);
        await NotifyPlanChangedAsync(subscription.UserId, ct);
    }

    /// <summary>
    /// Processes a subscription.canceled webhook event.
    /// </summary>
    public async Task HandleSubscriptionCanceledAsync(
        PaddleSubscription sub,
        string eventId,
        CancellationToken ct = default
    )
    {
        logger.LogInformation("Processing Paddle subscription.canceled event {EventId}", eventId);

        if (sub?.Id is null)
            return;

        if (await IsEventAlreadyProcessed(eventId, ct))
        {
            logger.LogDebug("Event {EventId} already processed; skipping", eventId);
            return;
        }

        var subscription = await db.Subscriptions.FirstOrDefaultAsync(
            s => s.PaddleSubscriptionId == sub.Id,
            ct
        );

        if (subscription is null)
        {
            await RecordProcessedEvent(eventId, "subscription.canceled", ct);
            return;
        }

        subscription.Status = SubscriptionStatus.Canceled;
        subscription.UpdatedAtUtc = DateTime.UtcNow;
        subscription.CancelledAtUtc = sub.CanceledAt?.ToUniversalTime();

        if (sub.ScheduledChange?.Action is not null)
            subscription.CancellationReason = sub.ScheduledChange.Action;

        await db.SaveChangesAsync(ct);
        await RecordProcessedEvent(eventId, "subscription.canceled", ct);
        logger.LogInformation("Completed Paddle event {EventId}", eventId);
        await NotifyPlanChangedAsync(subscription.UserId, ct);
    }

    /// <summary>
    /// Processes a transaction.completed webhook event.
    /// </summary>
    public async Task HandleTransactionCompletedAsync(
        PaddleTransaction txn,
        string eventId,
        CancellationToken ct = default
    )
    {
        logger.LogInformation("Processing Paddle transaction.completed event {EventId}", eventId);

        if (txn?.SubscriptionId is null)
            return;

        if (await IsEventAlreadyProcessed(eventId, ct))
        {
            logger.LogDebug("Event {EventId} already processed; skipping", eventId);
            return;
        }

        var subscription = await db.Subscriptions.FirstOrDefaultAsync(
            s => s.PaddleSubscriptionId == txn.SubscriptionId,
            ct
        );

        if (subscription is null)
        {
            var userId = ResolveUserIdFromTransaction(txn);
            if (userId is null)
            {
                logger.LogError(
                    "transaction.completed event {EventId}: no subscription row found for {SubId} and no user_id in custom_data",
                    eventId,
                    txn.SubscriptionId
                );
                await RecordProcessedEvent(eventId, "transaction.completed", ct);
                return;
            }

            if (!await UserExistsAsync(userId.Value, ct))
            {
                logger.LogError(
                    "transaction.completed event {EventId}: User {UserId} not provisioned in database",
                    eventId,
                    userId.Value
                );
                await RecordProcessedEvent(eventId, "transaction.completed", ct);
                return;
            }

            subscription = await db.Subscriptions.FirstOrDefaultAsync(
                s => s.UserId == userId.Value,
                ct
            );

            if (subscription is not null)
            {
                logger.LogWarning(
                    "transaction.completed event {EventId}: merging into existing subscription row for user {UserId} (paddle subscription {SubId})",
                    eventId,
                    userId.Value,
                    txn.SubscriptionId
                );
                subscription.PaddleSubscriptionId = txn.SubscriptionId;
                subscription.PaddleCustomerId = txn.CustomerId;
                subscription.Status = SubscriptionStatus.Active;
                var mergedPlan = MapPlanTypeFromTransaction(txn);
                subscription.PlanType = mergedPlan ?? PlanType.CloudBYOK;
            }
            else
            {
                logger.LogWarning(
                    "transaction.completed event {EventId}: arrived before subscription.created for {SubId}; creating row from transaction data",
                    eventId,
                    txn.SubscriptionId
                );

                var newPlan = MapPlanTypeFromTransaction(txn);
                subscription = new Subscription
                {
                    UserId = userId.Value,
                    PaddleSubscriptionId = txn.SubscriptionId,
                    PaddleCustomerId = txn.CustomerId,
                    Status = SubscriptionStatus.Active,
                    PlanType = newPlan ?? PlanType.CloudBYOK,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow,
                };
                db.Subscriptions.Add(subscription);
            }
        }

        if (!string.IsNullOrEmpty(txn.Id))
            subscription.PaddleTransactionId = txn.Id;

        if (txn.BillingPeriod?.EndsAt.HasValue == true)
            subscription.CurrentPeriodEndsAtUtc = txn.BillingPeriod.EndsAt.Value.ToUniversalTime();

        ApplyPaymentDetails(subscription, txn.Payments);
        subscription.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await RecordProcessedEvent(eventId, "transaction.completed", ct);
        logger.LogInformation("Completed Paddle event {EventId}", eventId);
        await NotifyPlanChangedAsync(subscription.UserId, ct);
    }

    private static void ApplyPaymentDetails(
        Subscription subscription,
        List<PaddlePayment>? payments
    )
    {
        if (payments is null || payments.Count == 0)
            return;

        var payment = payments[0];
        var details = payment.MethodDetails;

        if (details?.Type is not null)
            subscription.PaymentMethodType = details.Type;

        if (details?.Card?.Last4 is not null)
            subscription.CardLastFour = details.Card.Last4;
    }

    private static bool TryResolveUserId(PaddleCustomData? customData, out Guid userId)
    {
        userId = default;
        if (customData?.UserId is null)
            return false;

        return Guid.TryParse(customData.UserId, out userId);
    }

    private static SubscriptionStatus MapSubscriptionStatus(string? paddleStatus)
    {
        return paddleStatus switch
        {
            "active" => SubscriptionStatus.Active,
            "trialing" => SubscriptionStatus.Trial,
            "past_due" => SubscriptionStatus.Expired,
            "paused" => SubscriptionStatus.Expired,
            "canceled" => SubscriptionStatus.Canceled,
            _ => SubscriptionStatus.None,
        };
    }

    private static PlanType? MapPlanType(
        PaddleCustomData? customData,
        List<PaddleSubscriptionItem>? items
    )
    {
        if (customData?.PlanType is not null)
        {
            return customData.PlanType switch
            {
                "cloud-byok" => PlanType.CloudBYOK,
                "cloud-managed" => PlanType.CloudManaged,
                _ => null,
            };
        }

        var firstPrice = items?.FirstOrDefault()?.Price;
        if (firstPrice?.CustomData is not null)
        {
            var planType = firstPrice.CustomData.PlanType;
            if (planType is not null)
            {
                return planType switch
                {
                    "cloud-byok" => PlanType.CloudBYOK,
                    "cloud-managed" => PlanType.CloudManaged,
                    _ => null,
                };
            }

            var license = firstPrice.CustomData.License;
            if (license is not null)
            {
                return license switch
                {
                    "byok" => PlanType.CloudBYOK,
                    "premium" => PlanType.CloudManaged,
                    _ => null,
                };
            }
        }

        return null;
    }

    private static PlanType? MapPlanTypeFromTransaction(PaddleTransaction txn)
    {
        if (txn.Items is null || txn.Items.Count == 0)
            return MapPlanType(txn.CustomData, null);

        var mappedItems = txn
            .Items.Select(i => new PaddleSubscriptionItem { Price = i.Price })
            .ToList();
        return MapPlanType(txn.CustomData, mappedItems);
    }

    private static void ApplySubscriptionEnrichment(
        Subscription subscription,
        PaddleSubscription sub
    )
    {
        var firstPrice = sub.Items?.FirstOrDefault()?.Price;

        if (firstPrice is not null)
        {
            subscription.PaddlePriceId = firstPrice.Id;
            subscription.PaddleProductId = firstPrice.ProductId;

            if (firstPrice.UnitPrice is not null)
            {
                if (
                    long.TryParse(
                        firstPrice.UnitPrice.Amount,
                        CultureInfo.InvariantCulture,
                        out var minor
                    )
                )
                    subscription.UnitAmountMinor = minor;

                subscription.CurrencyCode = firstPrice.UnitPrice.CurrencyCode;
            }

            if (firstPrice.BillingCycle?.Interval is not null)
                subscription.BillingInterval = firstPrice.BillingCycle.Interval;
        }

        if (sub.CurrentBillingPeriod?.EndsAt.HasValue == true)
            subscription.CurrentPeriodEndsAtUtc =
                sub.CurrentBillingPeriod.EndsAt.Value.ToUniversalTime();

        if (sub.NextBilledAt.HasValue)
            subscription.NextBilledAtUtc = sub.NextBilledAt.Value.ToUniversalTime();
    }

    private async Task NotifyPlanChangedAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            await hub.Clients.Group(userId.ToString()).PlanChanged(new PlanChangedEvent());
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push PlanChanged for user {UserId}", userId);
        }
    }

    private async Task<bool> IsEventAlreadyProcessed(string eventId, CancellationToken ct)
    {
        return await db.ProcessedWebhookEvents.AnyAsync(e => e.EventId == eventId, ct);
    }

    private async Task RecordProcessedEvent(string eventId, string eventType, CancellationToken ct)
    {
        db.ProcessedWebhookEvents.Add(
            new ProcessedWebhookEvent
            {
                EventId = eventId,
                EventType = eventType,
                ProcessedAtUtc = DateTime.UtcNow,
            }
        );
        await db.SaveChangesAsync(ct);
    }

    private static Guid? ResolveUserIdFromTransaction(PaddleTransaction txn)
    {
        if (txn.CustomData?.UserId is null)
            return null;

        if (Guid.TryParse(txn.CustomData.UserId, out var userId))
            return userId;

        return null;
    }
}

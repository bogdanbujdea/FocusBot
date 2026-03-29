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
    ILogger<SubscriptionService> logger)
{
    /// <summary>
    /// Returns the current subscription status for a user.
    /// </summary>
    public async Task<SubscriptionStatusResponse> GetStatusAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var subscription = await db
            .Subscriptions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (subscription is null)
            return new SubscriptionStatusResponse("none", PlanType.FreeBYOK, null, null, null);

        return new SubscriptionStatusResponse(
            subscription.Status,
            subscription.PlanType,
            subscription.TrialEndsAtUtc,
            subscription.CurrentPeriodEndsAtUtc,
            subscription.NextBilledAtUtc);
    }

    /// <summary>
    /// Activates a 24-hour trial for a user. Returns null if the user already has a subscription record
    /// (trial already used or subscription exists).
    /// </summary>
    public async Task<ActivateTrialResponse?> ActivateTrialAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var existing = await db.Subscriptions.FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (existing is not null)
            return null;

        var trialEnd = DateTime.UtcNow.AddHours(24);
        var subscription = new Subscription
        {
            UserId = userId,
            Status = "trial",
            TrialEndsAtUtc = trialEnd,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync(ct);

        return new ActivateTrialResponse("trial", trialEnd);
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
            "active" => true,
            "trial" => subscription.TrialEndsAtUtc > DateTime.UtcNow,
            _ => false,
        };
    }

    /// <summary>
    /// Opens a Paddle customer portal session for the authenticated user.
    /// </summary>
    public async Task<string?> CreateCustomerPortalUrlAsync(Guid userId, CancellationToken ct = default)
    {
        var subscription = await db
            .Subscriptions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (subscription?.PaddleCustomerId is null)
            return null;

        return await paddleBilling.CreateCustomerPortalSessionAsync(
            subscription.PaddleCustomerId,
            subscription.PaddleSubscriptionId,
            ct);
    }

    /// <summary>
    /// Processes verified Paddle webhook events.
    /// </summary>
    public async Task HandlePaddleWebhookAsync(JsonElement payload, CancellationToken ct = default)
    {
        PaddleWebhookPayload? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<PaddleWebhookPayload>(payload.GetRawText());
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize Paddle webhook envelope");
            return;
        }

        if (envelope?.EventType is null || envelope.Data is null)
            return;

        switch (envelope.EventType)
        {
            case "subscription.created":
            case "subscription.updated":
            case "subscription.canceled":
                await HandleSubscriptionEvent(envelope, ct);
                break;
            case "transaction.completed":
                await HandleTransactionCompleted(envelope, ct);
                break;
        }
    }

    private async Task HandleSubscriptionEvent(PaddleWebhookPayload envelope, CancellationToken ct)
    {
        PaddleSubscription? sub;
        try
        {
            var dataJson = JsonSerializer.Serialize(envelope.Data);
            sub = JsonSerializer.Deserialize<PaddleSubscription>(dataJson);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize Paddle subscription data");
            return;
        }

        if (sub?.Id is null)
            return;

        var eventType = envelope.EventType;

        if (eventType == "subscription.created")
        {
            if (!TryResolveUserId(sub.CustomData, out var userId))
                return;

            var mappedPlan = MapPlanType(sub.CustomData, sub.Items);
            var mappedStatus = MapSubscriptionStatus(sub.Status);

            var existing = await db.Subscriptions.FirstOrDefaultAsync(s => s.UserId == userId, ct);

            if (existing is not null)
            {
                existing.PaddleSubscriptionId = sub.Id;
                existing.PaddleCustomerId = sub.CustomerId;
                existing.Status = mappedStatus;
                if (mappedPlan.HasValue)
                    existing.PlanType = mappedPlan.Value;
                existing.UpdatedAtUtc = DateTime.UtcNow;
                ApplySubscriptionEnrichment(existing, sub);
            }
            else
            {
                var planType = mappedPlan ?? PlanType.CloudBYOK;
                var created = new Subscription
                {
                    UserId = userId,
                    PaddleSubscriptionId = sub.Id,
                    PaddleCustomerId = sub.CustomerId,
                    Status = mappedStatus,
                    PlanType = planType,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow,
                };
                ApplySubscriptionEnrichment(created, sub);
                db.Subscriptions.Add(created);
            }

            await db.SaveChangesAsync(ct);
            await NotifyPlanChangedAsync(userId, ct);
        }
        else if (eventType == "subscription.updated")
        {
            var subscription = await db.Subscriptions.FirstOrDefaultAsync(
                s => s.PaddleSubscriptionId == sub.Id,
                ct);

            if (subscription is null)
                return;

            subscription.Status = MapSubscriptionStatus(sub.Status);

            var mapped = MapPlanType(sub.CustomData, sub.Items);
            if (mapped.HasValue)
                subscription.PlanType = mapped.Value;

            ApplySubscriptionEnrichment(subscription, sub);
            subscription.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await NotifyPlanChangedAsync(subscription.UserId, ct);
        }
        else if (eventType == "subscription.canceled")
        {
            var subscription = await db.Subscriptions.FirstOrDefaultAsync(
                s => s.PaddleSubscriptionId == sub.Id,
                ct);

            if (subscription is null)
                return;

            subscription.Status = "canceled";
            subscription.UpdatedAtUtc = DateTime.UtcNow;

            if (envelope.OccurredAt.HasValue)
                subscription.CancelledAtUtc = envelope.OccurredAt.Value.ToUniversalTime();

            if (sub.ScheduledChange?.Action is not null)
                subscription.CancellationReason = sub.ScheduledChange.Action;

            await db.SaveChangesAsync(ct);
            await NotifyPlanChangedAsync(subscription.UserId, ct);
        }
    }

    private async Task HandleTransactionCompleted(PaddleWebhookPayload envelope, CancellationToken ct)
    {
        PaddleTransaction? txn;
        try
        {
            var dataJson = JsonSerializer.Serialize(envelope.Data);
            txn = JsonSerializer.Deserialize<PaddleTransaction>(dataJson);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize Paddle transaction data");
            return;
        }

        if (txn?.SubscriptionId is null)
            return;

        var subscription = await db.Subscriptions.FirstOrDefaultAsync(
            s => s.PaddleSubscriptionId == txn.SubscriptionId,
            ct);

        if (subscription is null)
        {
            logger.LogInformation(
                "Transaction {TxnId} references subscription {SubId} not yet in DB; likely race with subscription.created",
                txn.Id, txn.SubscriptionId);
            return;
        }

        if (!string.IsNullOrEmpty(txn.Id))
            subscription.PaddleTransactionId = txn.Id;

        if (txn.BillingPeriod?.EndsAt.HasValue == true)
            subscription.CurrentPeriodEndsAtUtc = txn.BillingPeriod.EndsAt.Value.ToUniversalTime();

        ApplyPaymentDetails(subscription, txn.Payments);
        subscription.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await NotifyPlanChangedAsync(subscription.UserId, ct);
    }

    private static void ApplyPaymentDetails(Subscription subscription, List<PaddlePayment>? payments)
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

    private static string MapSubscriptionStatus(string? paddleStatus)
    {
        return paddleStatus switch
        {
            "active" => "active",
            "trialing" => "trial",
            "past_due" => "active",
            "paused" => "expired",
            "canceled" => "canceled",
            _ => paddleStatus ?? "none",
        };
    }

    private static PlanType? MapPlanType(PaddleCustomData? customData, List<PaddleSubscriptionItem>? items)
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

    private static void ApplySubscriptionEnrichment(Subscription subscription, PaddleSubscription sub)
    {
        var firstPrice = sub.Items?.FirstOrDefault()?.Price;

        if (firstPrice is not null)
        {
            subscription.PaddlePriceId = firstPrice.Id;
            subscription.PaddleProductId = firstPrice.ProductId;

            if (firstPrice.UnitPrice is not null)
            {
                if (long.TryParse(firstPrice.UnitPrice.Amount, CultureInfo.InvariantCulture, out var minor))
                    subscription.UnitAmountMinor = minor;

                subscription.CurrencyCode = firstPrice.UnitPrice.CurrencyCode;
            }

            if (firstPrice.BillingCycle?.Interval is not null)
                subscription.BillingInterval = firstPrice.BillingCycle.Interval;
        }

        if (sub.CurrentBillingPeriod?.EndsAt.HasValue == true)
            subscription.CurrentPeriodEndsAtUtc = sub.CurrentBillingPeriod.EndsAt.Value.ToUniversalTime();

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
}

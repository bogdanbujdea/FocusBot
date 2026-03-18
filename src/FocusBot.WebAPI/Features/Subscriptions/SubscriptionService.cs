using System.Text.Json;
using FocusBot.WebAPI.Data;
using FocusBot.WebAPI.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FocusBot.WebAPI.Features.Subscriptions;

/// <summary>
/// Business logic for subscription lifecycle, trial activation, and Paddle webhook handling.
/// </summary>
public class SubscriptionService(ApiDbContext db)
{
    /// <summary>
    /// Returns the current subscription status for a user.
    /// </summary>
    public async Task<SubscriptionStatusResponse> GetStatusAsync(Guid userId, CancellationToken ct = default)
    {
        var subscription = await db.Subscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (subscription is null)
            return new SubscriptionStatusResponse("none", null, null);

        return new SubscriptionStatusResponse(
            subscription.Status,
            subscription.TrialEndsAtUtc,
            subscription.CurrentPeriodEndsAtUtc);
    }

    /// <summary>
    /// Activates a 24-hour trial for a user. Returns null if the user already has a subscription record
    /// (trial already used or subscription exists).
    /// </summary>
    public async Task<ActivateTrialResponse?> ActivateTrialAsync(Guid userId, CancellationToken ct = default)
    {
        var existing = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (existing is not null)
            return null;

        var trialEnd = DateTime.UtcNow.AddHours(24);
        var subscription = new Subscription
        {
            UserId = userId,
            Status = "trial",
            TrialEndsAtUtc = trialEnd,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync(ct);

        return new ActivateTrialResponse("trial", trialEnd);
    }

    /// <summary>
    /// Checks whether the user has an active subscription or a trial that has not expired.
    /// </summary>
    public async Task<bool> IsSubscribedOrTrialActiveAsync(Guid userId, CancellationToken ct = default)
    {
        var subscription = await db.Subscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (subscription is null)
            return false;

        return subscription.Status switch
        {
            "active" => true,
            "trial" => subscription.TrialEndsAtUtc > DateTime.UtcNow,
            _ => false
        };
    }

    /// <summary>
    /// Processes Paddle webhook events. Simplified for MVP without cryptographic verification.
    /// </summary>
    // TODO: Add Paddle webhook signature verification for production
    public async Task HandlePaddleWebhookAsync(JsonElement payload, CancellationToken ct = default)
    {
        if (!payload.TryGetProperty("event_type", out var eventTypeProp))
            return;

        var eventType = eventTypeProp.GetString();

        switch (eventType)
        {
            case "subscription.created":
                await HandleSubscriptionCreated(payload, ct);
                break;
            case "subscription.updated":
                await HandleSubscriptionUpdated(payload, ct);
                break;
            case "subscription.canceled":
                await HandleSubscriptionCanceled(payload, ct);
                break;
            case "transaction.completed":
                await HandleTransactionCompleted(payload, ct);
                break;
        }
    }

    private async Task HandleSubscriptionCreated(JsonElement payload, CancellationToken ct)
    {
        var data = payload.GetProperty("data");
        var paddleSubId = data.GetProperty("id").GetString()!;
        var paddleCustomerId = data.TryGetProperty("customer_id", out var custProp)
            ? custProp.GetString()
            : null;

        var customData = data.TryGetProperty("custom_data", out var customProp) ? customProp : (JsonElement?)null;
        if (customData is null || !customData.Value.TryGetProperty("user_id", out var userIdProp))
            return;

        if (!Guid.TryParse(userIdProp.GetString(), out var userId))
            return;

        var existing = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (existing is not null)
        {
            existing.PaddleSubscriptionId = paddleSubId;
            existing.PaddleCustomerId = paddleCustomerId;
            existing.Status = "active";
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            db.Subscriptions.Add(new Subscription
            {
                UserId = userId,
                PaddleSubscriptionId = paddleSubId,
                PaddleCustomerId = paddleCustomerId,
                Status = "active",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task HandleSubscriptionUpdated(JsonElement payload, CancellationToken ct)
    {
        var data = payload.GetProperty("data");
        var paddleSubId = data.GetProperty("id").GetString()!;

        var subscription = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.PaddleSubscriptionId == paddleSubId, ct);

        if (subscription is null)
            return;

        if (data.TryGetProperty("status", out var statusProp))
        {
            var paddleStatus = statusProp.GetString();
            subscription.Status = paddleStatus switch
            {
                "active" => "active",
                "past_due" => "active",
                "paused" => "expired",
                "canceled" => "canceled",
                _ => subscription.Status
            };
        }

        subscription.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task HandleSubscriptionCanceled(JsonElement payload, CancellationToken ct)
    {
        var data = payload.GetProperty("data");
        var paddleSubId = data.GetProperty("id").GetString()!;

        var subscription = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.PaddleSubscriptionId == paddleSubId, ct);

        if (subscription is null)
            return;

        subscription.Status = "canceled";
        subscription.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task HandleTransactionCompleted(JsonElement payload, CancellationToken ct)
    {
        var data = payload.GetProperty("data");

        if (!data.TryGetProperty("subscription_id", out var subIdProp))
            return;

        var paddleSubId = subIdProp.GetString();
        if (string.IsNullOrEmpty(paddleSubId))
            return;

        var subscription = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.PaddleSubscriptionId == paddleSubId, ct);

        if (subscription is null)
            return;

        if (data.TryGetProperty("billing_period", out var billingPeriod) &&
            billingPeriod.TryGetProperty("ends_at", out var endsAtProp))
        {
            if (DateTime.TryParse(endsAtProp.GetString(), out var endsAt))
                subscription.CurrentPeriodEndsAtUtc = endsAt.ToUniversalTime();
        }

        subscription.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}

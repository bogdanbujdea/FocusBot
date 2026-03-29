using System.Text.Json.Serialization;

namespace FocusBot.WebAPI.Features.Subscriptions;

/// <summary>
/// Paddle webhook envelope (all event types).
/// </summary>
public sealed class PaddleWebhookPayload
{
    [JsonPropertyName("event_id")]
    public string EventId { get; set; } = "";

    [JsonPropertyName("event_type")]
    public string EventType { get; set; } = "";

    [JsonPropertyName("occurred_at")]
    public DateTime? OccurredAt { get; set; }

    [JsonPropertyName("notification_id")]
    public string? NotificationId { get; set; }

    [JsonPropertyName("data")]
    public object? Data { get; set; }
}

/// <summary>
/// Paddle subscription entity (for subscription.created, subscription.updated, subscription.canceled).
/// </summary>
public sealed class PaddleSubscription
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("customer_id")]
    public string? CustomerId { get; set; }

    [JsonPropertyName("custom_data")]
    public PaddleCustomData? CustomData { get; set; }

    [JsonPropertyName("items")]
    public List<PaddleSubscriptionItem>? Items { get; set; }

    [JsonPropertyName("next_billed_at")]
    public DateTime? NextBilledAt { get; set; }

    [JsonPropertyName("current_billing_period")]
    public PaddleBillingPeriod? CurrentBillingPeriod { get; set; }

    [JsonPropertyName("scheduled_change")]
    public PaddleScheduledChange? ScheduledChange { get; set; }

    [JsonPropertyName("billing_cycle")]
    public PaddleBillingCycle? BillingCycle { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [JsonPropertyName("paused_at")]
    public DateTime? PausedAt { get; set; }

    [JsonPropertyName("canceled_at")]
    public DateTime? CanceledAt { get; set; }
}

/// <summary>
/// Paddle transaction entity (for transaction.completed).
/// </summary>
public sealed class PaddleTransaction
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("subscription_id")]
    public string? SubscriptionId { get; set; }

    [JsonPropertyName("custom_data")]
    public PaddleCustomData? CustomData { get; set; }

    [JsonPropertyName("items")]
    public List<PaddleTransactionItem>? Items { get; set; }

    [JsonPropertyName("billing_period")]
    public PaddleBillingPeriod? BillingPeriod { get; set; }

    [JsonPropertyName("payments")]
    public List<PaddlePayment>? Payments { get; set; }

    [JsonPropertyName("customer_id")]
    public string? CustomerId { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("billed_at")]
    public DateTime? BilledAt { get; set; }
}

public sealed class PaddleCustomData
{
    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("plan_type")]
    public string? PlanType { get; set; }
}

public sealed class PaddleSubscriptionItem
{
    [JsonPropertyName("price")]
    public PaddlePrice? Price { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }
}

public sealed class PaddleTransactionItem
{
    [JsonPropertyName("price")]
    public PaddlePrice? Price { get; set; }

    [JsonPropertyName("price_id")]
    public string? PriceId { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
}

public sealed class PaddlePrice
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("product_id")]
    public string? ProductId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("unit_price")]
    public PaddleUnitPrice? UnitPrice { get; set; }

    [JsonPropertyName("billing_cycle")]
    public PaddleBillingCycle? BillingCycle { get; set; }

    [JsonPropertyName("custom_data")]
    public PaddlePriceCustomData? CustomData { get; set; }
}

public sealed class PaddlePriceCustomData
{
    [JsonPropertyName("plan_type")]
    public string? PlanType { get; set; }

    [JsonPropertyName("license")]
    public string? License { get; set; }
}

public sealed class PaddleUnitPrice
{
    [JsonPropertyName("amount")]
    public string Amount { get; set; } = "";

    [JsonPropertyName("currency_code")]
    public string CurrencyCode { get; set; } = "";
}

public sealed class PaddleBillingCycle
{
    [JsonPropertyName("interval")]
    public string Interval { get; set; } = "";

    [JsonPropertyName("frequency")]
    public int Frequency { get; set; }
}

public sealed class PaddleBillingPeriod
{
    [JsonPropertyName("starts_at")]
    public DateTime? StartsAt { get; set; }

    [JsonPropertyName("ends_at")]
    public DateTime? EndsAt { get; set; }
}

public sealed class PaddleScheduledChange
{
    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("effective_at")]
    public DateTime? EffectiveAt { get; set; }
}

public sealed class PaddlePayment
{
    [JsonPropertyName("amount")]
    public string? Amount { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("method_details")]
    public PaddlePaymentMethodDetails? MethodDetails { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("captured_at")]
    public DateTime? CapturedAt { get; set; }
}

public sealed class PaddlePaymentMethodDetails
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("card")]
    public PaddleCardDetails? Card { get; set; }
}

public sealed class PaddleCardDetails
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("last4")]
    public string? Last4 { get; set; }

    [JsonPropertyName("expiry_month")]
    public int? ExpiryMonth { get; set; }

    [JsonPropertyName("expiry_year")]
    public int? ExpiryYear { get; set; }

    [JsonPropertyName("cardholder_name")]
    public string? CardholderName { get; set; }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace FocusBot.WebAPI.Data.Entities;

/// <summary>
/// Tracks a user's subscription state including Paddle billing and 24-hour trial.
/// </summary>
public class Subscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string? PaddleSubscriptionId { get; set; }
    public string? PaddleCustomerId { get; set; }

    /// <summary>Billing/trial lifecycle status: None, Trial, Active, Expired, Canceled.</summary>
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.None;

    /// <summary>The subscription tier the user is on.</summary>
    public PlanType PlanType { get; set; } = PlanType.TrialFullAccess;

    public DateTime? TrialEndsAtUtc { get; set; }
    public DateTime? CurrentPeriodEndsAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;

    public string? PaddlePriceId { get; set; }
    public string? PaddleProductId { get; set; }
    public string? PaddleTransactionId { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CurrencyCode { get; set; }
    public long? UnitAmountMinor { get; set; }
    public string? BillingInterval { get; set; }
    public DateTime? NextBilledAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancellationReason { get; set; }
    public string? PaymentMethodType { get; set; }
    public string? CardLastFour { get; set; }
}

/// <summary>The Foqus subscription tiers.</summary>
public enum PlanType
{
    /// <summary>Generic 24h trial — plan not yet chosen by the user.</summary>
    TrialFullAccess = 0,
    CloudBYOK = 1,
    CloudManaged = 2,
}

/// <summary>Subscription lifecycle status.</summary>
[JsonConverter(typeof(CamelCaseEnumConverter<SubscriptionStatus>))]
public enum SubscriptionStatus
{
    None,
    Trial,
    Active,
    Expired,
    Canceled,
}

/// <summary>Custom JSON converter for enums that serializes as camelCase strings.</summary>
public sealed class CamelCaseEnumConverter<T> : JsonStringEnumConverter<T>
    where T : struct, Enum
{
    public CamelCaseEnumConverter() : base(JsonNamingPolicy.CamelCase)
    {
    }
}

/// <summary>Tracks processed webhook events for idempotency.</summary>
public class ProcessedWebhookEvent
{
    public string EventId { get; set; } = "";
    public string EventType { get; set; } = "";
    public DateTime ProcessedAtUtc { get; set; }
}

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

    /// <summary>Billing/trial lifecycle status: none, trial, active, expired, canceled.</summary>
    public string Status { get; set; } = "none";

    /// <summary>The subscription tier the user is on.</summary>
    public PlanType PlanType { get; set; } = PlanType.FreeBYOK;

    public DateTime? TrialEndsAtUtc { get; set; }
    public DateTime? CurrentPeriodEndsAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
}

/// <summary>The three Foqus subscription tiers.</summary>
public enum PlanType
{
    FreeBYOK = 0,
    CloudBYOK = 1,
    CloudManaged = 2,
}

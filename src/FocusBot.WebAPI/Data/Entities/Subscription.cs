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
    public string Status { get; set; } = "none"; // none, trial, active, expired, canceled
    public DateTime? TrialEndsAtUtc { get; set; }
    public DateTime? CurrentPeriodEndsAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
}

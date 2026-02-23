namespace FocusBot.Core.Entities;

/// <summary>
/// Information about the user's subscription status.
/// </summary>
public record SubscriptionInfo
{
    /// <summary>
    /// Whether the subscription is currently active.
    /// </summary>
    public required bool IsActive { get; init; }

    /// <summary>
    /// When the current billing period ends.
    /// </summary>
    public required DateTimeOffset ExpirationDate { get; init; }

    /// <summary>
    /// Whether the subscription will auto-renew. Null when unknown (Store API does not expose this for add-on licenses).
    /// </summary>
    public bool? WillAutoRenew { get; init; }

    /// <summary>
    /// True if the user is in a free trial period. Set from license when available; otherwise false.
    /// </summary>
    public bool IsTrialPeriod { get; init; }
}

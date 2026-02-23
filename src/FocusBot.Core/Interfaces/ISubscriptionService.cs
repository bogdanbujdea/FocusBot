using FocusBot.Core.Entities;

namespace FocusBot.Core.Interfaces;

/// <summary>
/// Service for managing subscription status via Windows Store.
/// </summary>
public interface ISubscriptionService
{
    /// <summary>
    /// Checks if the user has an active subscription.
    /// </summary>
    Task<bool> IsSubscribedAsync();

    /// <summary>
    /// Gets detailed subscription information.
    /// Returns null if not subscribed.
    /// </summary>
    Task<SubscriptionInfo?> GetSubscriptionInfoAsync();

    /// <summary>
    /// Opens the Windows Store purchase UI for the subscription.
    /// </summary>
    Task<PurchaseResult> PurchaseSubscriptionAsync();

    /// <summary>
    /// Opens the Microsoft account subscription management page.
    /// </summary>
    Task OpenManageSubscriptionAsync();
}

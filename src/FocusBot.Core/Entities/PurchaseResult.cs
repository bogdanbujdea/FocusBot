namespace FocusBot.Core.Entities;

/// <summary>
/// Result of a subscription purchase attempt.
/// </summary>
public enum PurchaseResult
{
    /// <summary>
    /// Purchase completed successfully.
    /// </summary>
    Success,

    /// <summary>
    /// User cancelled the purchase.
    /// </summary>
    Cancelled,

    /// <summary>
    /// User already has this subscription.
    /// </summary>
    AlreadyOwned,

    /// <summary>
    /// Network error during purchase.
    /// </summary>
    NetworkError,

    /// <summary>
    /// Unknown error occurred.
    /// </summary>
    Error
}

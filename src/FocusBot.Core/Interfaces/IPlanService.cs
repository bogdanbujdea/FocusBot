namespace FocusBot.Core.Interfaces;

/// <summary>
/// The three Foqus subscription tiers available to desktop clients.
/// Mirrors <c>FocusBot.WebAPI.Data.Entities.PlanType</c>.
/// </summary>
public enum ClientPlanType
{
    /// <summary>Trial full access (maps to server <c>PlanType.TrialFullAccess</c>).</summary>
    FreeBYOK = 0,

    /// <summary>User provides their own API key plus gets cloud analytics and cross-device sync.</summary>
    CloudBYOK = 1,

    /// <summary>Platform provides the API key. Full cloud analytics and cross-device sync.</summary>
    CloudManaged = 2,
}

/// <summary>
/// Subscription lifecycle status from <c>GET /subscriptions/status</c> (camelCase JSON).
/// </summary>
public enum ClientSubscriptionStatus
{
    None = 0,
    Trial = 1,
    Active = 2,
    Expired = 3,
    Canceled = 4,
}

/// <summary>
/// Manages the user's current subscription plan. Fetches from the backend and caches locally.
/// Replaces the deleted ISubscriptionService and ITrialService.
/// </summary>
public interface IPlanService
{
    /// <summary>
    /// Returns the cached plan. Fetches from backend on first call (or if cache is stale).
    /// </summary>
    Task<ClientPlanType> GetCurrentPlanAsync(CancellationToken ct = default);

    /// <summary>Forces a refresh from the backend.</summary>
    Task RefreshAsync(CancellationToken ct = default);

    /// <summary>Returns true if the user is on a cloud plan (CloudBYOK or CloudManaged).</summary>
    bool IsCloudPlan(ClientPlanType plan);

    /// <summary>
    /// Cached subscription status from the last successful fetch.
    /// </summary>
    Task<ClientSubscriptionStatus> GetStatusAsync(CancellationToken ct = default);

    /// <summary>
    /// Trial end time from the last successful fetch, when applicable.
    /// </summary>
    Task<DateTime?> GetTrialEndsAtAsync(CancellationToken ct = default);

    /// <summary>
    /// Current billing period end time from the last successful fetch, when available.
    /// </summary>
    Task<DateTime?> GetCurrentPeriodEndsAtAsync(CancellationToken ct = default);

    /// <summary>Raised when the plan changes after a refresh.</summary>
    event EventHandler<ClientPlanType>? PlanChanged;
}

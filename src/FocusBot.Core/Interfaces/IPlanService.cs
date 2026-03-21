namespace FocusBot.Core.Interfaces;

/// <summary>
/// The three Foqus subscription tiers available to desktop clients.
/// Mirrors <c>FocusBot.WebAPI.Data.Entities.PlanType</c>.
/// </summary>
public enum ClientPlanType
{
    /// <summary>User provides their own API key. Local basic analytics only. No cloud sync.</summary>
    FreeBYOK = 0,

    /// <summary>User provides their own API key plus gets cloud analytics and cross-device sync.</summary>
    CloudBYOK = 1,

    /// <summary>Platform provides the API key. Full cloud analytics and cross-device sync.</summary>
    CloudManaged = 2,
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

    /// <summary>Raised when the plan changes after a refresh.</summary>
    event EventHandler<ClientPlanType>? PlanChanged;
}

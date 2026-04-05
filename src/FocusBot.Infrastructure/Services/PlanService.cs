using FocusBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Manages the user's current subscription plan. Fetches from the backend and caches locally.
/// The cache is considered stale after <see cref="CacheDuration"/>.
/// </summary>
public class PlanService(
    IFocusBotApiClient apiClient,
    ISettingsService settings,
    ILogger<PlanService> logger) : IPlanService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private const string PlanTypeKey = "Plan_Type";
    private const string PlanCachedAtKey = "Plan_CachedAt";
    private const string PlanStatusKey = "Plan_SubscriptionStatus";
    private const string PlanTrialEndsAtKey = "Plan_TrialEndsAtUtc";
    private const string PlanCurrentPeriodEndsAtKey = "Plan_CurrentPeriodEndsAtUtc";

    private ClientPlanType? _memoryCached;
    private DateTime _memoryFetchedAt = DateTime.MinValue;
    private ClientSubscriptionStatus _memoryStatus = ClientSubscriptionStatus.None;
    private DateTime? _memoryTrialEndsAt;
    private DateTime? _memoryCurrentPeriodEndsAt;

    public event EventHandler<ClientPlanType>? PlanChanged;

    public async Task<ClientPlanType> GetCurrentPlanAsync(CancellationToken ct = default)
    {
        if (!apiClient.IsConfigured)
        {
            ClearMemoryCache();
            logger.LogDebug("Not authenticated; returning TrialFullAccess without consulting cache");
            return ClientPlanType.TrialFullAccess;
        }

        if (IsMemoryCacheFresh())
            return _memoryCached!.Value;

        var cached = await TryLoadFromSettingsAsync();
        if (cached.HasValue && IsFresh(cached.Value.fetchedAt))
        {
            UpdateMemoryCache(
                cached.Value.plan,
                cached.Value.status,
                cached.Value.trialEndsAt,
                cached.Value.currentPeriodEndsAt);
            return _memoryCached!.Value;
        }

        return await FetchAndCacheAsync(ct);
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        await FetchAndCacheAsync(ct);
    }

    public bool IsCloudPlan(ClientPlanType plan) =>
        plan is ClientPlanType.CloudBYOK or ClientPlanType.CloudManaged;

    public async Task<ClientSubscriptionStatus> GetStatusAsync(CancellationToken ct = default)
    {
        if (!apiClient.IsConfigured)
            return ClientSubscriptionStatus.None;

        if (IsMemoryCacheFresh())
            return _memoryStatus;

        var cached = await TryLoadFromSettingsAsync();
        if (cached.HasValue && IsFresh(cached.Value.fetchedAt))
        {
            UpdateMemoryCache(
                cached.Value.plan,
                cached.Value.status,
                cached.Value.trialEndsAt,
                cached.Value.currentPeriodEndsAt);
            return _memoryStatus;
        }

        await FetchAndCacheAsync(ct);
        return _memoryStatus;
    }

    public async Task<DateTime?> GetTrialEndsAtAsync(CancellationToken ct = default)
    {
        if (!apiClient.IsConfigured)
            return null;

        if (IsMemoryCacheFresh())
            return _memoryTrialEndsAt;

        var cached = await TryLoadFromSettingsAsync();
        if (cached.HasValue && IsFresh(cached.Value.fetchedAt))
        {
            UpdateMemoryCache(
                cached.Value.plan,
                cached.Value.status,
                cached.Value.trialEndsAt,
                cached.Value.currentPeriodEndsAt);
            return _memoryTrialEndsAt;
        }

        await FetchAndCacheAsync(ct);
        return _memoryTrialEndsAt;
    }

    public async Task<DateTime?> GetCurrentPeriodEndsAtAsync(CancellationToken ct = default)
    {
        if (!apiClient.IsConfigured)
            return null;

        if (IsMemoryCacheFresh())
            return _memoryCurrentPeriodEndsAt;

        var cached = await TryLoadFromSettingsAsync();
        if (cached.HasValue && IsFresh(cached.Value.fetchedAt))
        {
            UpdateMemoryCache(
                cached.Value.plan,
                cached.Value.status,
                cached.Value.trialEndsAt,
                cached.Value.currentPeriodEndsAt);
            return _memoryCurrentPeriodEndsAt;
        }

        await FetchAndCacheAsync(ct);
        return _memoryCurrentPeriodEndsAt;
    }

    private bool IsMemoryCacheFresh() =>
        _memoryCached.HasValue && IsFresh(_memoryFetchedAt);

    private static bool IsFresh(DateTime fetchedAt) =>
        DateTime.UtcNow - fetchedAt < CacheDuration;

    private async Task<(ClientPlanType plan, ClientSubscriptionStatus status, DateTime? trialEndsAt, DateTime? currentPeriodEndsAt, DateTime fetchedAt)?>
        TryLoadFromSettingsAsync()
    {
        try
        {
            var planRaw = await settings.GetSettingAsync<int?>(PlanTypeKey);
            var fetchedAtRaw = await settings.GetSettingAsync<DateTime?>(PlanCachedAtKey);
            var statusRaw = await settings.GetSettingAsync<int?>(PlanStatusKey);
            var trialEndsRaw = await settings.GetSettingAsync<DateTime?>(PlanTrialEndsAtKey);
            var currentPeriodEndsRaw = await settings.GetSettingAsync<DateTime?>(PlanCurrentPeriodEndsAtKey);

            if (planRaw.HasValue && fetchedAtRaw.HasValue)
            {
                var status = statusRaw.HasValue
                    ? (ClientSubscriptionStatus)statusRaw.Value
                    : ClientSubscriptionStatus.None;
                return ((ClientPlanType)planRaw.Value, status, trialEndsRaw, currentPeriodEndsRaw, fetchedAtRaw.Value);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read cached plan from settings");
        }

        return null;
    }

    private async Task<ClientPlanType> FetchAndCacheAsync(CancellationToken ct)
    {
        if (!apiClient.IsConfigured)
        {
            var fallback = _memoryCached ?? ClientPlanType.TrialFullAccess;
            logger.LogDebug("Not authenticated; returning fallback plan {Plan}", fallback);
            return fallback;
        }

        try
        {
            var status = await apiClient.GetSubscriptionStatusAsync();
            if (status is null)
            {
                logger.LogWarning("GetSubscriptionStatus returned null; keeping existing plan");
                return _memoryCached ?? ClientPlanType.TrialFullAccess;
            }

            var newPlan = (ClientPlanType)status.PlanType;
            var newSubStatus = ParseSubscriptionStatus(status.Status);
            var trialEnds = status.TrialEndsAt;
            var currentPeriodEnds = status.CurrentPeriodEndsAt;
            var previous = _memoryCached;

            UpdateMemoryCache(newPlan, newSubStatus, trialEnds, currentPeriodEnds);
            await PersistPlanAsync(newPlan, newSubStatus, trialEnds, currentPeriodEnds);

            if (previous.HasValue && previous.Value != newPlan)
            {
                logger.LogInformation("Plan changed from {Previous} to {New}", previous, newPlan);
                PlanChanged?.Invoke(this, newPlan);
            }

            return newPlan;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch plan from backend; keeping existing plan");
            return _memoryCached ?? ClientPlanType.TrialFullAccess;
        }
    }

    internal static ClientSubscriptionStatus ParseSubscriptionStatus(string? status) =>
        status?.Trim().ToLowerInvariant() switch
        {
            "trial" => ClientSubscriptionStatus.Trial,
            "active" => ClientSubscriptionStatus.Active,
            "expired" => ClientSubscriptionStatus.Expired,
            "canceled" => ClientSubscriptionStatus.Canceled,
            _ => ClientSubscriptionStatus.None,
        };

    private void UpdateMemoryCache(
        ClientPlanType plan,
        ClientSubscriptionStatus subscriptionStatus,
        DateTime? trialEndsAt,
        DateTime? currentPeriodEndsAt)
    {
        _memoryCached = plan;
        _memoryFetchedAt = DateTime.UtcNow;
        _memoryStatus = subscriptionStatus;
        _memoryTrialEndsAt = trialEndsAt;
        _memoryCurrentPeriodEndsAt = currentPeriodEndsAt;
    }

    private void ClearMemoryCache()
    {
        _memoryCached = null;
        _memoryFetchedAt = DateTime.MinValue;
        _memoryStatus = ClientSubscriptionStatus.None;
        _memoryTrialEndsAt = null;
        _memoryCurrentPeriodEndsAt = null;
    }

    private async Task PersistPlanAsync(
        ClientPlanType plan,
        ClientSubscriptionStatus subscriptionStatus,
        DateTime? trialEndsAt,
        DateTime? currentPeriodEndsAt)
    {
        try
        {
            await settings.SetSettingAsync(PlanTypeKey, (int)plan);
            await settings.SetSettingAsync(PlanCachedAtKey, DateTime.UtcNow);
            await settings.SetSettingAsync(PlanStatusKey, (int)subscriptionStatus);
            await settings.SetSettingAsync(PlanTrialEndsAtKey, trialEndsAt);
            await settings.SetSettingAsync(PlanCurrentPeriodEndsAtKey, currentPeriodEndsAt);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist plan to settings");
        }
    }
}

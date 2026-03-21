using FocusBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Manages the user's current subscription plan. Fetches from the backend and caches locally.
/// The cache is considered stale after <see cref="CacheDuration"/>; stale reads trigger a
/// background refresh so callers always get a fast response.
/// </summary>
public class PlanService(
    IFocusBotApiClient apiClient,
    ISettingsService settings,
    ILogger<PlanService> logger) : IPlanService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private const string PlanTypeKey = "Plan_Type";
    private const string PlanCachedAtKey = "Plan_CachedAt";

    private ClientPlanType? _memoryCached;
    private DateTime _memoryFetchedAt = DateTime.MinValue;

    public event EventHandler<ClientPlanType>? PlanChanged;

    public async Task<ClientPlanType> GetCurrentPlanAsync(CancellationToken ct = default)
    {
        if (!apiClient.IsConfigured)
        {
            ClearMemoryCache();
            logger.LogDebug("Not authenticated; returning FreeBYOK without consulting cache");
            return ClientPlanType.FreeBYOK;
        }

        if (IsMemoryCacheFresh())
            return _memoryCached!.Value;

        var cached = await TryLoadFromSettingsAsync();
        if (cached.HasValue && IsFresh(cached.Value.fetchedAt))
        {
            UpdateMemoryCache(cached.Value.plan);
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

    private bool IsMemoryCacheFresh() =>
        _memoryCached.HasValue && IsFresh(_memoryFetchedAt);

    private static bool IsFresh(DateTime fetchedAt) =>
        DateTime.UtcNow - fetchedAt < CacheDuration;

    private async Task<(ClientPlanType plan, DateTime fetchedAt)?> TryLoadFromSettingsAsync()
    {
        try
        {
            var planRaw = await settings.GetSettingAsync<int?>(PlanTypeKey);
            var fetchedAtRaw = await settings.GetSettingAsync<DateTime?>(PlanCachedAtKey);

            if (planRaw.HasValue && fetchedAtRaw.HasValue)
                return ((ClientPlanType)planRaw.Value, fetchedAtRaw.Value);
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
            var fallback = _memoryCached ?? ClientPlanType.FreeBYOK;
            logger.LogDebug("Not authenticated; returning fallback plan {Plan}", fallback);
            return fallback;
        }

        try
        {
            var status = await apiClient.GetSubscriptionStatusAsync();
            if (status is null)
            {
                logger.LogWarning("GetSubscriptionStatus returned null; keeping existing plan");
                return _memoryCached ?? ClientPlanType.FreeBYOK;
            }

            var newPlan = (ClientPlanType)status.PlanType;
            var previous = _memoryCached;

            UpdateMemoryCache(newPlan);
            await PersistPlanAsync(newPlan);

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
            return _memoryCached ?? ClientPlanType.FreeBYOK;
        }
    }

    private void UpdateMemoryCache(ClientPlanType plan)
    {
        _memoryCached = plan;
        _memoryFetchedAt = DateTime.UtcNow;
    }

    private void ClearMemoryCache()
    {
        _memoryCached = null;
        _memoryFetchedAt = DateTime.MinValue;
    }

    private async Task PersistPlanAsync(ClientPlanType plan)
    {
        try
        {
            await settings.SetSettingAsync(PlanTypeKey, (int)plan);
            await settings.SetSettingAsync(PlanCachedAtKey, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist plan to settings");
        }
    }
}

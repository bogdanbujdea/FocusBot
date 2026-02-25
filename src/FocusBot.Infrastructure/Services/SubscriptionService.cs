using Windows.Services.Store;
using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Manages subscription status using Windows Store APIs.
/// </summary>
public class SubscriptionService : ISubscriptionService
{
    /// <summary>
    /// Store ID from Partner Center (Subscription overview). Override with env FOCUSBOT_SUBSCRIPTION_STORE_ID for testing.
    /// </summary>
    private const string DefaultSubscriptionStoreId = "9P6D9DGTVXLR";

    private static string GetSubscriptionStoreId()
    {
        var env = Environment.GetEnvironmentVariable("FOCUSBOT_SUBSCRIPTION_STORE_ID");
        return !string.IsNullOrWhiteSpace(env) ? env.Trim() : DefaultSubscriptionStoreId;
    }

    private readonly ILogger<SubscriptionService> _logger;
    private readonly StoreContextHolder _contextHolder;
    private readonly IUIThreadDispatcher _uiDispatcher;

    public SubscriptionService(
        ILogger<SubscriptionService> logger,
        StoreContextHolder contextHolder,
        IUIThreadDispatcher uiDispatcher)
    {
        _logger = logger;
        _contextHolder = contextHolder;
        _uiDispatcher = uiDispatcher;
    }

    private StoreContext GetStoreContext()
    {
        if (_contextHolder.Context != null)
            return _contextHolder.Context;
        var ctx = StoreContext.GetDefault();
        _logger.LogWarning("StoreContext was not initialized with window HWND; purchase UI may not display correctly");
        return ctx;
    }

    public async Task<bool> IsSubscribedAsync()
    {
        try
        {
            var info = await GetSubscriptionInfoAsync();
            return info?.IsActive ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check subscription status");
            return false;
        }
    }

    public async Task<SubscriptionInfo?> GetSubscriptionInfoAsync()
    {
        try
        {
            var context = GetStoreContext();
            var license = await context.GetAppLicenseAsync();

            var storeId = GetSubscriptionStoreId();
            _logger.LogDebug("AddOnLicenses keys: {Keys}", string.Join(", ", license.AddOnLicenses.Keys));

            StoreLicense? addOnLicense = null;

            // First try exact match (bare product ID)
            if (!license.AddOnLicenses.TryGetValue(storeId, out addOnLicense))
            {
                // Fallback: find by prefix (handles productId/skuId keys)
                var match = license.AddOnLicenses
                    .FirstOrDefault(kvp => kvp.Key.StartsWith(storeId + "/", StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(match.Key))
                    addOnLicense = match.Value;
            }

            if (addOnLicense == null)
                return null;

            var isTrial = TryGetIsTrialFromLicense(addOnLicense);

            return new SubscriptionInfo
            {
                IsActive = addOnLicense.IsActive,
                ExpirationDate = addOnLicense.ExpirationDate,
                WillAutoRenew = null,
                IsTrialPeriod = isTrial
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get subscription info");
            return null;
        }
    }

    public async Task<PurchaseResult> PurchaseSubscriptionAsync()
    {
        try
        {
            var storeId = GetSubscriptionStoreId();
            StorePurchaseResult? purchaseResult = null;
            await _uiDispatcher.RunOnUIThreadAsync(async () =>
            {
                var context = GetStoreContext();
                purchaseResult = await context.RequestPurchaseAsync(storeId);
            });

            return purchaseResult!.Status switch
            {
                StorePurchaseStatus.Succeeded => PurchaseResult.Success,
                StorePurchaseStatus.AlreadyPurchased => PurchaseResult.AlreadyOwned,
                StorePurchaseStatus.NotPurchased => PurchaseResult.Cancelled,
                StorePurchaseStatus.NetworkError => PurchaseResult.NetworkError,
                StorePurchaseStatus.ServerError => PurchaseResult.Error,
                _ => PurchaseResult.Error
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to purchase subscription");
            return PurchaseResult.Error;
        }
    }

    public Task OpenManageSubscriptionAsync()
    {
        var uri = new Uri("ms-windows-store://account/subscriptions");
        return Windows.System.Launcher.LaunchUriAsync(uri).AsTask();
    }

    private static bool TryGetIsTrialFromLicense(StoreLicense license)
    {
        try
        {
            var json = license.ExtendedJsonData;
            if (string.IsNullOrWhiteSpace(json))
                return false;
            if (json.IndexOf("isTrial", StringComparison.OrdinalIgnoreCase) >= 0
                && (json.Contains("\"isTrial\":true", StringComparison.Ordinal) || json.Contains("'isTrial':true", StringComparison.Ordinal)))
                return true;
        }
        catch
        {
            // ignore parse errors
        }

        return false;
    }
}

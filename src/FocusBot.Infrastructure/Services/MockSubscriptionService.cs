using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Mock subscription service for development without Partner Center setup.
/// Simulates a short purchase delay so the loading state is visible.
/// Optional: set env MOCK_SUBSCRIPTION_RESULT=Success|Cancelled|NetworkError|Error to test different outcomes.
/// </summary>
public class MockSubscriptionService : ISubscriptionService
{
    private const int SimulatedPurchaseDelayMs = 1500;

    private bool _isSubscribed;

    public Task<bool> IsSubscribedAsync() => Task.FromResult(_isSubscribed);

    public Task<SubscriptionInfo?> GetSubscriptionInfoAsync()
    {
        if (!_isSubscribed)
            return Task.FromResult<SubscriptionInfo?>(null);

        return Task.FromResult<SubscriptionInfo?>(new SubscriptionInfo
        {
            IsActive = true,
            ExpirationDate = DateTimeOffset.Now.AddDays(30),
            WillAutoRenew = true,
            IsTrialPeriod = false
        });
    }

    public async Task<PurchaseResult> PurchaseSubscriptionAsync()
    {
        await Task.Delay(SimulatedPurchaseDelayMs);

        var result = GetMockResult();
        if (result == PurchaseResult.Success || result == PurchaseResult.AlreadyOwned)
            _isSubscribed = true;

        return result;
    }

    private static PurchaseResult GetMockResult()
    {
        var env = Environment.GetEnvironmentVariable("MOCK_SUBSCRIPTION_RESULT");
        if (string.IsNullOrWhiteSpace(env))
            return PurchaseResult.Success;

        return env.Trim().ToUpperInvariant() switch
        {
            "CANCELLED" => PurchaseResult.Cancelled,
            "NETWORKERROR" => PurchaseResult.NetworkError,
            "ERROR" => PurchaseResult.Error,
            "ALREADYOWNED" => PurchaseResult.AlreadyOwned,
            _ => PurchaseResult.Success
        };
    }

    public Task OpenManageSubscriptionAsync()
    {
        var uri = new Uri("ms-windows-store://account/subscriptions");
        return Windows.System.Launcher.LaunchUriAsync(uri).AsTask();
    }
}

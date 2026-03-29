namespace FocusBot.WebAPI.Features.Pricing;

/// <summary>
/// Paddle Billing HTTP operations (prices list, customer portal).
/// </summary>
public interface IPaddleBillingApi
{
    Task<PricingResponse?> GetPricingAsync(CancellationToken ct = default);

    Task<string?> CreateCustomerPortalSessionAsync(
        string paddleCustomerId,
        string? paddleSubscriptionId,
        CancellationToken ct = default);
}

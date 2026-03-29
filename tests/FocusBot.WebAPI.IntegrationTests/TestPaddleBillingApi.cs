using FocusBot.WebAPI.Features.Pricing;

namespace FocusBot.WebAPI.IntegrationTests;

/// <summary>
/// Stub Paddle API for integration tests (no outbound HTTP).
/// </summary>
internal sealed class TestPaddleBillingApi : IPaddleBillingApi
{
    public Task<PricingResponse?> GetPricingAsync(CancellationToken ct = default) =>
        Task.FromResult<PricingResponse?>(
            new PricingResponse(
                [
                    new PricingPlanDto(
                        "pri_test_byok",
                        "Cloud BYOK",
                        "Test",
                        199,
                        "USD",
                        "month",
                        "cloud-byok"),
                    new PricingPlanDto(
                        "pri_test_managed",
                        "Cloud Managed",
                        "Test",
                        499,
                        "USD",
                        "month",
                        "cloud-managed"),
                ],
                "test_client_token",
                true));

    public Task<string?> CreateCustomerPortalSessionAsync(
        string paddleCustomerId,
        string? paddleSubscriptionId,
        CancellationToken ct = default) =>
        Task.FromResult<string?>("https://example.com/customer-portal");
}

namespace FocusBot.WebAPI.Features.Pricing;

/// <summary>
/// Public pricing proxy for Paddle.js (price ids, client token, sandbox flag).
/// </summary>
public static class PricingEndpoints
{
    public static void MapPricingEndpoints(this WebApplication app)
    {
        app.MapGet(
                "/pricing",
                async (IPaddleBillingApi paddle, CancellationToken ct) =>
                {
                    var pricing = await paddle.GetPricingAsync(ct);
                    return pricing is null
                        ? Results.Problem(
                            statusCode: StatusCodes.Status503ServiceUnavailable,
                            detail: "Pricing is temporarily unavailable.")
                        : Results.Ok(pricing);
                })
            .AllowAnonymous()
            .WithTags("Pricing")
            .WithName("GetPricing")
            .WithSummary("List active Paddle prices and client token for checkout");
    }
}

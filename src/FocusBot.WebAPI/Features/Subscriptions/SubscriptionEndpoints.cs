using System.Security.Claims;
using System.Text.Json;

namespace FocusBot.WebAPI.Features.Subscriptions;

/// <summary>
/// Minimal API endpoints for subscription management and Paddle webhook intake.
/// </summary>
public static class SubscriptionEndpoints
{
    public static void MapSubscriptionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/subscriptions")
            .WithTags("Subscriptions");

        group.MapGet("/status", async (
            SubscriptionService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = GetUserId(ctx);
            var status = await service.GetStatusAsync(userId, ct);
            return Results.Ok(status);
        })
        .RequireAuthorization()
        .WithName("GetSubscriptionStatus")
        .WithSummary("Get current subscription status for the authenticated user");

        group.MapPost("/trial", async (
            SubscriptionService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = GetUserId(ctx);
            var result = await service.ActivateTrialAsync(userId, ct);

            return result is null
                ? Results.Conflict(new { error = "Trial already activated or subscription exists." })
                : Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("ActivateTrial")
        .WithSummary("Activate a 24-hour free trial");

        // TODO: Add Paddle webhook signature verification for production
        group.MapPost("/paddle-webhook", async (
            JsonElement payload,
            SubscriptionService service,
            CancellationToken ct) =>
        {
            await service.HandlePaddleWebhookAsync(payload, ct);
            return Results.Ok();
        })
        .AllowAnonymous()
        .WithName("PaddleWebhook")
        .WithSummary("Receive Paddle billing webhook events");
    }

    private static Guid GetUserId(HttpContext ctx)
    {
        var sub = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? ctx.User.FindFirstValue("sub")
                  ?? throw new InvalidOperationException("JWT missing sub claim");
        return Guid.Parse(sub);
    }
}

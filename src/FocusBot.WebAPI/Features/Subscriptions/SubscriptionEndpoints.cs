using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FocusBot.WebAPI;
using FocusBot.WebAPI.Features.Pricing;
using Microsoft.Extensions.Options;

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

        group.MapPost("/portal", async (
            SubscriptionService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = GetUserId(ctx);
            var url = await service.CreateCustomerPortalUrlAsync(userId, ct);
            return url is null
                ? Results.BadRequest(new { error = "No Paddle customer linked to this account." })
                : Results.Ok(new CustomerPortalResponse(url));
        })
        .RequireAuthorization()
        .WithName("CreateCustomerPortalSession")
        .WithSummary("Create a Paddle customer portal session URL");

        group.MapPost("/paddle-webhook", HandlePaddleWebhook)
            .AllowAnonymous()
            .WithName("PaddleWebhook")
            .WithSummary("Receive Paddle billing webhook events");
    }

    private static async Task<IResult> HandlePaddleWebhook(
        HttpContext http,
        IOptions<PaddleSettings> paddleSettings,
        SubscriptionService service,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        http.Request.EnableBuffering();
        string rawBody;
        using (var reader = new StreamReader(http.Request.Body, Encoding.UTF8, leaveOpen: true))
            rawBody = await reader.ReadToEndAsync(ct);
        http.Request.Body.Position = 0;

        var log = loggerFactory.CreateLogger("PaddleWebhook");
        var secret = paddleSettings.Value.WebhookSecret;
        var sig = http.Request.Headers["Paddle-Signature"].FirstOrDefault();

        if (!PaddleWebhookVerifier.TryVerify(rawBody, sig, secret, out var verifyMessage))
        {
            log.LogWarning("Paddle webhook rejected: {Reason}", verifyMessage);
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (!string.IsNullOrEmpty(verifyMessage))
            log.LogInformation("{Message}", verifyMessage);

        using var doc = JsonDocument.Parse(rawBody);
        await service.HandlePaddleWebhookAsync(doc.RootElement, ct);
        return Results.Ok();
    }

    private static Guid GetUserId(HttpContext ctx)
    {
        var sub = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? ctx.User.FindFirstValue("sub")
                  ?? throw new InvalidOperationException("JWT missing sub claim");
        return Guid.Parse(sub);
    }
}

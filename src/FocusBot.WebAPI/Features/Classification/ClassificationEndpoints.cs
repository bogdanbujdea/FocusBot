using System.Security.Claims;
using FocusBot.WebAPI.Features.Subscriptions;

namespace FocusBot.WebAPI.Features.Classification;

/// <summary>
/// Minimal API endpoints for AI focus-alignment classification.
/// </summary>
public static class ClassificationEndpoints
{
    public static RouteGroupBuilder MapClassificationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/classify")
            .WithTags("Classification")
            .RequireAuthorization();

        group.MapPost("/", async (
            ClassifyRequest request,
            ClassificationService service,
            SubscriptionService subscriptionService,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var sub = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? ctx.User.FindFirstValue("sub");

            if (sub is null || !Guid.TryParse(sub, out var userId))
                return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.TaskText))
                return Results.BadRequest("TaskText is required.");

            var byokApiKey = ctx.Request.Headers["X-Api-Key"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(byokApiKey))
            {
                var isActive = await subscriptionService.IsSubscribedOrTrialActiveAsync(userId, ct);
                if (!isActive)
                    return Results.StatusCode(402);
            }

            try
            {
                var result = await service.ClassifyAsync(userId, request, byokApiKey, ct);
                return Results.Ok(result);
            }
            catch (ClassificationProviderException ex)
            {
                return ex.Code switch
                {
                    ClassificationErrorCode.InvalidKey => Results.Problem(
                        detail: ex.Message, statusCode: 401, title: "invalid_key"),
                    ClassificationErrorCode.RateLimited => Results.Problem(
                        detail: ex.Message, statusCode: 429, title: "rate_limited"),
                    _ => Results.Problem(
                        detail: ex.Message, statusCode: 502, title: "provider_unavailable"),
                };
            }
            catch (InvalidOperationException ex)
            {
                return Results.UnprocessableEntity(new { error = ex.Message });
            }
        })
        .WithName("Classify")
        .WithSummary("Classify focus alignment of the current window/tab against the user's task");

        group.MapPost("/validate-key", async (
            ValidateKeyRequest request,
            ClassificationService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.ApiKey))
                return Results.BadRequest("ApiKey is required.");

            var result = await service.ValidateKeyAsync(request.ProviderId, request.ModelId, request.ApiKey, ct);
            return Results.Ok(result);
        })
        .WithName("ValidateKey")
        .WithSummary("Validate a BYOK API key by making a minimal test request to the LLM provider");

        return group;
    }
}

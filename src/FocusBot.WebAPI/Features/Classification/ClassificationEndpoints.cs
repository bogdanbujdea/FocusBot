using System.Security.Claims;
using FocusBot.WebAPI.Features.Subscriptions;
using Microsoft.Extensions.Logging;

namespace FocusBot.WebAPI.Features.Classification;

/// <summary>
/// Minimal API endpoints for AI focus-alignment classification.
/// </summary>
public static class ClassificationEndpoints
{
    public static RouteGroupBuilder MapClassificationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/classify").WithTags("Classification").RequireAuthorization();

        group
            .MapPost(
                "/",
                async (
                    ClassifyRequest request,
                    ClassificationCoalescingService coalescingService,
                    SubscriptionService subscriptionService,
                    HttpContext ctx,
                    ILoggerFactory loggerFactory,
                    CancellationToken ct
                ) =>
                {
                    var logger = loggerFactory.CreateLogger("FocusBot.WebAPI.Features.Classification.ClassificationEndpoints");
                    var sub =
                        ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? ctx.User.FindFirstValue("sub");

                    if (sub is null || !Guid.TryParse(sub, out var userId))
                        return Results.Unauthorized();

                    if (string.IsNullOrWhiteSpace(request.SessionTitle))
                        return Results.BadRequest("SessionTitle is required.");

                    var byokApiKey = ctx.Request.Headers["X-Api-Key"].FirstOrDefault();

                    if (string.IsNullOrWhiteSpace(byokApiKey))
                    {
                        var isActive = await subscriptionService.IsSubscribedOrTrialActiveAsync(
                            userId,
                            ct
                        );
                        if (!isActive)
                            return Results.StatusCode(402);
                    }

                    try
                    {
                        var remoteIp = GetRemoteIpAddress(ctx);
                        var result = await coalescingService.EnqueueAndWaitAsync(
                            userId,
                            request,
                            byokApiKey,
                            remoteIp,
                            ct
                        );
                        logger.LogInformation(
                            "Classification response: UserId={UserId} Score={Score} Cached={Cached} Url={Url} WindowTitle={WindowTitle} PageTitle={PageTitle} Reason={Reason}",
                            userId,
                            result.Score,
                            result.Cached,
                            request.Url,
                            request.WindowTitle,
                            request.PageTitle,
                            result.Reason);
                        return Results.Ok(result);
                    }
                    catch (ClassificationProviderException ex)
                    {
                        return ex.Code switch
                        {
                            ClassificationErrorCode.InvalidKey => Results.Problem(
                                detail: ex.Message,
                                statusCode: 401,
                                title: "invalid_key"
                            ),
                            ClassificationErrorCode.RateLimited => Results.Problem(
                                detail: ex.Message,
                                statusCode: 429,
                                title: "rate_limited"
                            ),
                            _ => Results.Problem(
                                detail: ex.Message,
                                statusCode: 502,
                                title: "provider_unavailable"
                            ),
                        };
                    }
                    catch (InvalidOperationException ex)
                    {
                        return Results.UnprocessableEntity(new { error = ex.Message });
                    }
                }
            )
            .WithName("Classify")
            .WithSummary(
                "Classify focus alignment of the current window/tab against the user's session"
            );

        group
            .MapPost(
                "/validate-key",
                async (
                    ValidateKeyRequest request,
                    ClassificationService service,
                    CancellationToken ct
                ) =>
                {
                    if (string.IsNullOrWhiteSpace(request.ApiKey))
                        return Results.BadRequest("ApiKey is required.");

                    var result = await service.ValidateKeyAsync(
                        request.ProviderId,
                        request.ModelId,
                        request.ApiKey,
                        ct
                    );
                    return Results.Ok(result);
                }
            )
            .WithName("ValidateKey")
            .WithSummary(
                "Validate a BYOK API key by making a minimal test request to the LLM provider"
            );

        return group;
    }

    private static string? GetRemoteIpAddress(HttpContext ctx)
    {
        var ip = ctx.Connection.RemoteIpAddress;
        if (ip is null)
            return null;

        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            && ip.IsIPv4MappedToIPv6)
        {
            return ip.MapToIPv4().ToString();
        }

        return ip.ToString();
    }
}

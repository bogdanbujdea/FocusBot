using System.Security.Claims;
using FocusBot.WebAPI.Features.Clients;
using FocusBot.WebAPI.Features.Subscriptions;
using FocusBot.WebAPI.Hubs;
using Microsoft.AspNetCore.SignalR;

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
                    ClassificationService classificationService,
                    ClientService clientService,
                    SubscriptionService subscriptionService,
                    IHubContext<FocusHub, IFocusHubClient> hubContext,
                    HttpContext ctx,
                    ILoggerFactory loggerFactory,
                    CancellationToken ct
                ) =>
                {
                    var logger = loggerFactory.CreateLogger(
                        "FocusBot.WebAPI.Features.Classification.ClassificationEndpoints"
                    );
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
                        logger.LogInformation($"Classifying: {request.WindowTitle}\n");
                        var result = await classificationService.ClassifyAsync(
                            userId,
                            request,
                            byokApiKey,
                            ct
                        );

                        logger.LogInformation(
                            "Classification response:\n Url={Url}\nWindowTitle={WindowTitle}\nPageTitle={PageTitle}\nReason={Reason}\n\n",
                            request.Url,
                            request.WindowTitle,
                            result.Score,
                            result.Reason
                        );

                        await TouchClientLastSeenAsync(
                            clientService,
                            userId,
                            request,
                            remoteIp,
                            logger
                        );
                        await BroadcastClassificationAsync(
                            hubContext,
                            userId,
                            request,
                            result,
                            logger
                        );

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

    private static async Task TouchClientLastSeenAsync(
        ClientService clientService,
        Guid userId,
        ClassifyRequest request,
        string? remoteIp,
        ILogger logger
    )
    {
        if (request.ClientId is null)
            return;

        try
        {
            await clientService.TouchLastSeenAsync(
                userId,
                request.ClientId.Value,
                remoteIp,
                CancellationToken.None
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to touch last seen for client {ClientId}",
                request.ClientId
            );
        }
    }

    private static async Task BroadcastClassificationAsync(
        IHubContext<FocusHub, IFocusHubClient> hubContext,
        Guid userId,
        ClassifyRequest request,
        ClassifyResponse result,
        ILogger logger
    )
    {
        var (source, activityName) = ClassificationBroadcastHelper.Describe(request);
        var evt = new ClassificationChangedEvent(
            result.Score,
            result.Reason,
            source,
            activityName,
            DateTime.UtcNow,
            result.Cached
        );

        var classification =
            result.Score > 5 ? "Aligned"
            : result.Score < 5 ? "Distracting"
            : "Neutral";
        logger.LogInformation(
            "Broadcasting classification: {Classification} (score={Score}) from {Source} | Activity: {Activity} | Cached: {Cached}",
            classification,
            result.Score,
            source,
            activityName,
            result.Cached
        );

        try
        {
            await hubContext.Clients.Group(userId.ToString()).ClassificationChanged(evt);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to broadcast ClassificationChanged for user {UserId}",
                userId
            );
        }
    }

    private static string? GetRemoteIpAddress(HttpContext ctx)
    {
        var ip = ctx.Connection.RemoteIpAddress;
        if (ip is null)
            return null;

        if (
            ip is
            {
                AddressFamily: System.Net.Sockets.AddressFamily.InterNetworkV6,
                IsIPv4MappedToIPv6: true
            }
        )
        {
            return ip.MapToIPv4().ToString();
        }

        return ip.ToString();
    }
}

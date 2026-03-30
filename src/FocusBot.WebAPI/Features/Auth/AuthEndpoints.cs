using System.Security.Claims;
using FocusBot.WebAPI.Features.Subscriptions;

namespace FocusBot.WebAPI.Features.Auth;

/// <summary>
/// Minimal API endpoints for authentication and user profile.
/// </summary>
public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth").RequireAuthorization();

        group
            .MapGet(
                "/me",
                async (
                    AuthService authService,
                    SubscriptionService subscriptionService,
                    HttpContext ctx,
                    CancellationToken ct
                ) =>
                {
                    var user = await authService.GetOrProvisionUserAsync(ctx.User, ct);
                    var status = await subscriptionService.GetStatusAsync(user.Id, ct);
                    return Results.Ok(
                        new MeResponse(
                            user.Id,
                            user.Email,
                            status!.Status,
                            status.PlanType
                        )
                    );
                }
            )
            .WithName("GetMe")
            .WithSummary("Returns the current user's profile and subscription plan");

        group
            .MapDelete(
                "/account",
                async (AccountService accountService, HttpContext ctx, CancellationToken ct) =>
                {
                    var userId = GetUserId(ctx);
                    await accountService.DeleteAccountAsync(userId, ct);
                    return Results.Ok(new { message = "Account and all associated data deleted." });
                }
            )
            .WithName("DeleteAccount")
            .WithSummary("Permanently delete the authenticated user's account and all data");

        return group;
    }

    private static Guid GetUserId(HttpContext ctx)
    {
        var sub =
            ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? ctx.User.FindFirstValue("sub")
            ?? throw new InvalidOperationException("JWT missing sub claim");
        return Guid.Parse(sub);
    }
}

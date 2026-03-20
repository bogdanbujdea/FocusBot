using FocusBot.WebAPI.Features.Subscriptions;

namespace FocusBot.WebAPI.Features.Auth;

/// <summary>
/// Minimal API endpoints for authentication and user profile.
/// </summary>
public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth")
            .WithTags("Auth")
            .RequireAuthorization();

        group.MapGet("/me", async (
            AuthService authService,
            SubscriptionService subscriptionService,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var user = await authService.GetOrProvisionUserAsync(ctx.User, ct);
            var status = await subscriptionService.GetStatusAsync(user.Id, ct);
            return Results.Ok(new MeResponse(user.Id, user.Email, status.Status, status.PlanType));
        })
        .WithName("GetMe")
        .WithSummary("Returns the current user's profile and subscription plan");

        return group;
    }
}

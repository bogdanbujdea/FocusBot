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

        group.MapGet("/me", async (AuthService authService, HttpContext ctx, CancellationToken ct) =>
        {
            var user = await authService.GetOrProvisionUserAsync(ctx.User, ct);
            return Results.Ok(new MeResponse(user.Id, user.Email, "none"));
        })
        .WithName("GetMe")
        .WithSummary("Returns the current user's profile");

        return group;
    }
}

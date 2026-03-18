using Microsoft.AspNetCore.RateLimiting;

namespace FocusBot.WebAPI.Features.Waitlist;

public static class WaitlistEndpoints
{
    public static RouteGroupBuilder MapWaitlistEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api")
            .WithTags("Waitlist");

        group.MapPost("/waitlist", async (
            WaitlistSignupRequest request,
            HttpContext httpContext,
            WaitlistService service,
            CancellationToken ct) =>
        {
            if (!request.IsHoneypotEmpty())
            {
                // Pretend success to avoid giving bots a signal.
                return Results.Accepted();
            }

            if (!WaitlistEmailValidator.TryNormalize(request.Email, out var normalizedEmail))
            {
                return Results.BadRequest(new { error = "Invalid email address." });
            }

            await service.UpsertWaitlistSubscriberAsync(normalizedEmail, httpContext, ct);
            return Results.Accepted();
        })
        .AllowAnonymous()
        .RequireRateLimiting("Waitlist")
        .WithName("WaitlistSignup")
        .WithSummary("Join the Foqus waitlist (MailerLite).");

        return group;
    }
}


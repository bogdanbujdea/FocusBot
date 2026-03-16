using System.Security.Claims;

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

            try
            {
                var result = await service.ClassifyAsync(userId, request, byokApiKey, ct);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.UnprocessableEntity(new { error = ex.Message });
            }
        })
        .WithName("Classify")
        .WithSummary("Classify focus alignment of the current window/tab against the user's task");

        return group;
    }
}

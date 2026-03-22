using System.Security.Claims;

namespace FocusBot.WebAPI.Features.Sessions;

/// <summary>
/// Minimal API endpoints for focus session lifecycle.
/// </summary>
public static class SessionEndpoints
{
    public static RouteGroupBuilder MapSessionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/sessions")
            .WithTags("Sessions")
            .RequireAuthorization();

        group.MapPost("/", StartSession)
            .WithName("StartSession")
            .WithSummary("Start a new focus session");

        group.MapPost("/{id}/end", EndSession)
            .WithName("EndSession")
            .WithSummary("End an active focus session with summary data");

        group.MapPost("/{id}/pause", PauseSession)
            .WithName("PauseSession")
            .WithSummary("Pause an active focus session");

        group.MapPost("/{id}/resume", ResumeSession)
            .WithName("ResumeSession")
            .WithSummary("Resume a paused focus session");

        group.MapGet("/active", GetActiveSession)
            .WithName("GetActiveSession")
            .WithSummary("Get the current active focus session");

        group.MapGet("/", GetSessions)
            .WithName("GetSessions")
            .WithSummary("Get paginated completed session history");

        group.MapGet("/{id}", GetSessionById)
            .WithName("GetSessionById")
            .WithSummary("Get a single session by ID");

        return group;
    }

    private static async Task<IResult> StartSession(
        StartSessionRequest request, SessionService service, HttpContext ctx, CancellationToken ct)
    {
        var userId = GetUserId(ctx);
        var result = await service.StartSessionAsync(userId, request, ct);

        return result.StatusCode == 409
            ? Results.Conflict(new { error = result.Error })
            : Results.Created($"/sessions/{result.Session!.Id}", result.Session);
    }

    private static async Task<IResult> EndSession(
        Guid id, EndSessionRequest request, SessionService service, HttpContext ctx, CancellationToken ct)
    {
        var userId = GetUserId(ctx);
        var result = await service.EndSessionAsync(userId, id, request, ct);

        return result.StatusCode switch
        {
            403 => Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status403Forbidden),
            404 => Results.NotFound(new { error = result.Error }),
            409 => Results.Conflict(new { error = result.Error }),
            _ => Results.Ok(result.Session)
        };
    }

    private static async Task<IResult> PauseSession(
        Guid id, SessionService service, HttpContext ctx, CancellationToken ct)
    {
        var userId = GetUserId(ctx);
        var result = await service.PauseSessionAsync(userId, id, ct);

        return result.StatusCode switch
        {
            404 => Results.NotFound(new { error = result.Error }),
            409 => Results.Conflict(new { error = result.Error }),
            _ => Results.Ok(result.Session)
        };
    }

    private static async Task<IResult> ResumeSession(
        Guid id, SessionService service, HttpContext ctx, CancellationToken ct)
    {
        var userId = GetUserId(ctx);
        var result = await service.ResumeSessionAsync(userId, id, ct);

        return result.StatusCode switch
        {
            404 => Results.NotFound(new { error = result.Error }),
            409 => Results.Conflict(new { error = result.Error }),
            _ => Results.Ok(result.Session)
        };
    }

    private static async Task<IResult> GetActiveSession(
        SessionService service, HttpContext ctx, CancellationToken ct)
    {
        var userId = GetUserId(ctx);
        var session = await service.GetActiveSessionAsync(userId, ct);

        return session is not null ? Results.Ok(session) : Results.NotFound();
    }

    private static async Task<IResult> GetSessions(
        SessionService service, HttpContext ctx, int page = 1, int pageSize = 20,
        Guid? deviceId = null, DateTime? from = null, DateTime? to = null,
        string? sessionTitle = null, string sortBy = "startedAt", string sortOrder = "desc",
        CancellationToken ct = default)
    {
        var userId = GetUserId(ctx);
        var filter = new SessionFilter(deviceId, from, to, sessionTitle, sortBy, sortOrder);
        var result = await service.GetSessionsAsync(userId, page, pageSize, filter, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetSessionById(
        Guid id, SessionService service, HttpContext ctx, CancellationToken ct)
    {
        var userId = GetUserId(ctx);
        var session = await service.GetSessionByIdAsync(userId, id, ct);

        return session is not null ? Results.Ok(session) : Results.NotFound();
    }

    private static Guid GetUserId(HttpContext ctx)
    {
        var sub = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? ctx.User.FindFirstValue("sub")
                  ?? throw new InvalidOperationException("JWT missing sub claim");
        return Guid.Parse(sub);
    }
}

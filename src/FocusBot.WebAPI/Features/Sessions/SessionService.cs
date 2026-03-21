using FocusBot.WebAPI.Data;
using FocusBot.WebAPI.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FocusBot.WebAPI.Features.Sessions;

/// <summary>
/// Business logic for focus session lifecycle: start, end, query.
/// </summary>
public class SessionService(ApiDbContext db)
{
    public async Task<SessionResult> StartSessionAsync(
        Guid userId, StartSessionRequest request, CancellationToken ct = default)
    {
        var hasActive = await db.Sessions
            .AnyAsync(s => s.UserId == userId && s.EndedAtUtc == null, ct);

        if (hasActive)
            return SessionResult.Conflict("An active session already exists.");

        var session = new Session
        {
            UserId = userId,
            TaskText = request.TaskText,
            TaskHints = request.TaskHints,
            DeviceId = request.DeviceId,
            StartedAtUtc = DateTime.UtcNow,
            Source = "api"
        };

        db.Sessions.Add(session);
        await db.SaveChangesAsync(ct);

        return SessionResult.Success(ToResponse(session));
    }

    public async Task<SessionResult> EndSessionAsync(
        Guid userId, Guid sessionId, EndSessionRequest request, CancellationToken ct = default)
    {
        var session = await db.Sessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, ct);

        if (session is null)
            return SessionResult.NotFound();

        if (session.EndedAtUtc is not null)
            return SessionResult.Conflict("Session is already ended.");

        session.EndedAtUtc = DateTime.UtcNow;
        session.FocusScorePercent = request.FocusScorePercent;
        session.FocusedSeconds = request.FocusedSeconds;
        session.DistractedSeconds = request.DistractedSeconds;
        session.DistractionCount = request.DistractionCount;
        session.ContextSwitchCount = request.ContextSwitchCount;
        session.TopDistractingApps = request.TopDistractingApps;
        session.TopAlignedApps = request.TopAlignedApps;

        if (request.DeviceId.HasValue)
        {
            var deviceBelongsToUser = await db.Devices
                .AnyAsync(d => d.Id == request.DeviceId.Value && d.UserId == userId, ct);

            if (!deviceBelongsToUser)
                return SessionResult.Forbidden("Device does not belong to the current user.");

            session.DeviceId = request.DeviceId;
        }

        await db.SaveChangesAsync(ct);
        return SessionResult.Success(ToResponse(session));
    }

    public async Task<SessionResponse?> GetActiveSessionAsync(
        Guid userId, CancellationToken ct = default) =>
        await db.Sessions
            .Where(s => s.UserId == userId && s.EndedAtUtc == null)
            .Select(s => ToResponse(s))
            .FirstOrDefaultAsync(ct);

    public async Task<PaginatedResponse<SessionResponse>> GetSessionsAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Sessions
            .Where(s => s.UserId == userId && s.EndedAtUtc != null)
            .OrderByDescending(s => s.StartedAtUtc);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => ToResponse(s))
            .ToListAsync(ct);

        return new PaginatedResponse<SessionResponse>(items, totalCount, page, pageSize);
    }

    public async Task<SessionResponse?> GetSessionByIdAsync(
        Guid userId, Guid sessionId, CancellationToken ct = default) =>
        await db.Sessions
            .Where(s => s.Id == sessionId && s.UserId == userId)
            .Select(s => ToResponse(s))
            .FirstOrDefaultAsync(ct);

    private static SessionResponse ToResponse(Session s) =>
        new(s.Id, s.TaskText, s.TaskHints, s.DeviceId, s.StartedAtUtc, s.EndedAtUtc,
            s.FocusScorePercent, s.FocusedSeconds, s.DistractedSeconds,
            s.DistractionCount, s.ContextSwitchCount,
            s.TopDistractingApps, s.TopAlignedApps, s.Source);
}

/// <summary>Encapsulates the outcome of a session mutation operation.</summary>
public sealed class SessionResult
{
    public SessionResponse? Session { get; }
    public int StatusCode { get; }
    public string? Error { get; }

    private SessionResult(SessionResponse? session, int statusCode, string? error)
    {
        Session = session;
        StatusCode = statusCode;
        Error = error;
    }

    public static SessionResult Success(SessionResponse session) => new(session, 200, null);
    public static SessionResult Conflict(string error) => new(null, 409, error);
    public static SessionResult NotFound() => new(null, 404, "Session not found.");
    public static SessionResult Forbidden(string error) => new(null, 403, error);
}

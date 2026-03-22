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
        Guid userId,
        StartSessionRequest request,
        CancellationToken ct = default
    )
    {
        var hasActive = await db.Sessions.AnyAsync(
            s => s.UserId == userId && s.EndedAtUtc == null,
            ct
        );

        if (hasActive)
            return SessionResult.Conflict("An active session already exists.");

        var session = new Session
        {
            UserId = userId,
            SessionTitle = request.SessionTitle,
            Context = request.SessionContext,
            DeviceId = request.DeviceId,
            StartedAtUtc = DateTime.UtcNow,
            Source = "api",
        };

        db.Sessions.Add(session);
        await db.SaveChangesAsync(ct);

        return SessionResult.Success(ToResponse(session));
    }

    public async Task<SessionResult> EndSessionAsync(
        Guid userId,
        Guid sessionId,
        EndSessionRequest request,
        CancellationToken ct = default
    )
    {
        var session = await db.Sessions.FirstOrDefaultAsync(
            s => s.Id == sessionId && s.UserId == userId,
            ct
        );

        if (session is null)
            return SessionResult.NotFound();

        if (session.EndedAtUtc is not null)
            return SessionResult.Conflict("Session is already ended.");

        // If session is paused, accumulate final pause duration before ending
        if (session.PausedAtUtc is not null)
        {
            var pauseDuration = (long)(DateTime.UtcNow - session.PausedAtUtc.Value).TotalSeconds;
            session.TotalPausedSeconds += pauseDuration;
            session.PausedAtUtc = null;
        }

        session.EndedAtUtc = DateTime.UtcNow;
        session.FocusScorePercent = request.FocusScorePercent;
        session.FocusedSeconds = request.FocusedSeconds;
        session.DistractedSeconds = request.DistractedSeconds;
        session.DistractionCount = request.DistractionCount;
        session.ContextSwitchCount = request.ContextSwitchCount;

        if (request.DeviceId.HasValue)
        {
            var deviceBelongsToUser = await db.Devices.AnyAsync(
                d => d.Id == request.DeviceId.Value && d.UserId == userId,
                ct
            );

            if (!deviceBelongsToUser)
                return SessionResult.Forbidden("Device does not belong to the current user.");

            session.DeviceId = request.DeviceId;
        }

        await db.SaveChangesAsync(ct);
        return SessionResult.Success(ToResponse(session));
    }

    public async Task<SessionResponse?> GetActiveSessionAsync(
        Guid userId,
        CancellationToken ct = default
    ) =>
        await db
            .Sessions.Where(s => s.UserId == userId && s.EndedAtUtc == null)
            .Select(s => ToResponse(s))
            .FirstOrDefaultAsync(ct);

    public async Task<PaginatedResponse<SessionResponse>> GetSessionsAsync(
        Guid userId,
        int page,
        int pageSize,
        SessionFilter? filter = null,
        CancellationToken ct = default
    )
    {
        var baseQuery = db.Sessions.Where(s => s.UserId == userId && s.EndedAtUtc != null);

        if (filter?.DeviceId is not null)
            baseQuery = baseQuery.Where(s => s.DeviceId == filter.DeviceId);

        if (filter?.From is not null)
            baseQuery = baseQuery.Where(s => s.StartedAtUtc >= filter.From);

        if (filter?.To is not null)
            baseQuery = baseQuery.Where(s => s.StartedAtUtc < filter.To);

        if (!string.IsNullOrWhiteSpace(filter?.SessionTitle))
            baseQuery = baseQuery.Where(s => s.SessionTitle.Contains(filter.SessionTitle));

        var totalCount = await baseQuery.CountAsync(ct);

        var isAsc = string.Equals(filter?.SortOrder, "asc", StringComparison.OrdinalIgnoreCase);

        IOrderedQueryable<Data.Entities.Session> ordered = filter?.SortBy?.ToLowerInvariant() switch
        {
            "focusscore" => isAsc
                ? baseQuery.OrderBy(s => s.FocusScorePercent)
                : baseQuery.OrderByDescending(s => s.FocusScorePercent),
            "duration" => isAsc
                ? baseQuery.OrderBy(s => s.EndedAtUtc!.Value.Ticks - s.StartedAtUtc.Ticks)
                : baseQuery.OrderByDescending(s => s.EndedAtUtc!.Value.Ticks - s.StartedAtUtc.Ticks),
            _ => isAsc
                ? baseQuery.OrderBy(s => s.StartedAtUtc)
                : baseQuery.OrderByDescending(s => s.StartedAtUtc),
        };

        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => ToResponse(s))
            .ToListAsync(ct);

        return new PaginatedResponse<SessionResponse>(items, totalCount, page, pageSize);
    }

    public async Task<SessionResponse?> GetSessionByIdAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken ct = default
    ) =>
        await db
            .Sessions.Where(s => s.Id == sessionId && s.UserId == userId)
            .Select(s => ToResponse(s))
            .FirstOrDefaultAsync(ct);

    public async Task<SessionResult> PauseSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken ct = default
    )
    {
        var session = await db.Sessions.FirstOrDefaultAsync(
            s => s.Id == sessionId && s.UserId == userId,
            ct
        );

        if (session is null)
            return SessionResult.NotFound();

        if (session.EndedAtUtc is not null)
            return SessionResult.Conflict("Session is already ended.");

        if (session.PausedAtUtc is not null)
            return SessionResult.Conflict("Session is already paused.");

        session.PausedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return SessionResult.Success(ToResponse(session));
    }

    public async Task<SessionResult> ResumeSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken ct = default
    )
    {
        var session = await db.Sessions.FirstOrDefaultAsync(
            s => s.Id == sessionId && s.UserId == userId,
            ct
        );

        if (session is null)
            return SessionResult.NotFound();

        if (session.PausedAtUtc is null)
            return SessionResult.Conflict("Session is not paused.");

        var pauseDuration = (long)(DateTime.UtcNow - session.PausedAtUtc.Value).TotalSeconds;
        session.TotalPausedSeconds += pauseDuration;
        session.PausedAtUtc = null;

        await db.SaveChangesAsync(ct);

        return SessionResult.Success(ToResponse(session));
    }

    private static SessionResponse ToResponse(Session s) =>
        new(
            s.Id,
            s.SessionTitle,
            s.Context,
            s.DeviceId,
            s.StartedAtUtc,
            s.EndedAtUtc,
            s.PausedAtUtc,
            s.TotalPausedSeconds,
            s.IsPaused,
            s.FocusScorePercent,
            s.FocusedSeconds,
            s.DistractedSeconds,
            s.DistractionCount,
            s.ContextSwitchCount,
            s.Source
        );
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

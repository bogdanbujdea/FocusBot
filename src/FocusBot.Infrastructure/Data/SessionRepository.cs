using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FocusBot.Infrastructure.Data;

public class SessionRepository(AppDbContext context) : ISessionRepository
{
    public async Task<UserSession> AddSessionAsync(
        string description,
        string? sessionContext = null
    )
    {
        var session = new UserSession
        {
            SessionId = Guid.NewGuid().ToString(),
            Description = description,
            Context = string.IsNullOrWhiteSpace(sessionContext) ? null : sessionContext.Trim(),
            IsCompleted = false,
        };
        context.UserSessions.Add(session);
        await context.SaveChangesAsync();
        return session;
    }

    public async Task<UserSession?> GetByIdAsync(string sessionId) =>
        await context.UserSessions.FindAsync(sessionId);

    public async Task UpdateSessionDescriptionAsync(string sessionId, string newDescription)
    {
        var session = await context.UserSessions.FindAsync(sessionId);
        if (session != null)
        {
            session.Description = newDescription;
            await context.SaveChangesAsync();
        }
    }

    public async Task UpdateSessionAsync(
        string sessionId,
        string description,
        string? sessionContext
    )
    {
        var session = await context.UserSessions.FindAsync(sessionId);
        if (session != null)
        {
            session.Description = description;
            session.Context = string.IsNullOrWhiteSpace(sessionContext)
                ? null
                : sessionContext.Trim();
            await context.SaveChangesAsync();
        }
    }

    public async Task DeleteSessionAsync(string sessionId)
    {
        var session = await context.UserSessions.FindAsync(sessionId);
        if (session != null)
        {
            context.UserSessions.Remove(session);
            await context.SaveChangesAsync();
        }
    }

    public async Task SetActiveAsync(string sessionId)
    {
        var othersInProgress = await context
            .UserSessions.Where(t => !t.IsCompleted && t.SessionId != sessionId)
            .ToListAsync();
        foreach (var t in othersInProgress)
        {
            t.IsCompleted = true;
        }

        var session = await context.UserSessions.FindAsync(sessionId);
        if (session != null)
        {
            session.IsCompleted = false;
            await context.SaveChangesAsync();
        }
    }

    public async Task SetCompletedAsync(string sessionId)
    {
        var session = await context.UserSessions.FindAsync(sessionId);
        if (session != null)
        {
            session.IsCompleted = true;
            await context.SaveChangesAsync();
        }
    }

    public async Task UpdateElapsedTimeAsync(string sessionId, long totalElapsedSeconds)
    {
        var session = await context.UserSessions.FindAsync(sessionId);
        if (session != null)
        {
            session.TotalElapsedSeconds = totalElapsedSeconds;
            await context.SaveChangesAsync();
        }
    }

    public async Task<UserSession?> GetInProgressSessionAsync() =>
        await context
            .UserSessions.Where(t => !t.IsCompleted)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();

    public async Task<IEnumerable<UserSession>> GetDoneSessionsAsync() =>
        await context
            .UserSessions.Where(t => t.IsCompleted)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

    public async Task UpdateFocusScoreAsync(string sessionId, int scorePercent)
    {
        var session = await context.UserSessions.FindAsync(sessionId);
        if (session != null)
        {
            session.FocusScorePercent = scorePercent;
            await context.SaveChangesAsync();
        }
    }
}

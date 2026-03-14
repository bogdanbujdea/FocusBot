using FocusBot.Core.DTOs;
using FocusBot.Core.Interfaces;
using FocusBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FocusBot.Infrastructure.Services;

public sealed class SessionDistractionAnalyticsService : IDistractionAnalyticsService
{
    private readonly AppDbContext _context;

    public SessionDistractionAnalyticsService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<SessionDistractionSummary> GetSessionSummaryAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var events = await _context.DistractionEvents
            .Where(e => e.SessionId == sessionId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalCount = events.Count;

        var topApps = events
            .GroupBy(e => e.ProcessName)
            .Select(g => new AppDistractionSummary
            {
                AppName = g.Key,
                DistractionCount = g.Count(),
                DistractedDurationSeconds = g.Sum(x => x.DistractedDurationSecondsAtEmit)
            })
            .OrderByDescending(a => a.DistractedDurationSeconds)
            .ThenByDescending(a => a.DistractionCount)
            .ThenBy(a => a.AppName)
            .Take(3)
            .ToList();

        return new SessionDistractionSummary
        {
            TotalDistractionCount = totalCount,
            TopApps = topApps
        };
    }

    public async Task<int> GetLiveDistractionCountAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DistractionEvents
            .Where(e => e.SessionId == sessionId)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}


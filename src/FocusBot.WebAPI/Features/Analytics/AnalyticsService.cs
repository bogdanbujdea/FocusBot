using FocusBot.WebAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace FocusBot.WebAPI.Features.Analytics;

/// <summary>
/// Aggregation service for analytics endpoints. Computes metrics from completed sessions.
/// </summary>
public class AnalyticsService(ApiDbContext db)
{
    public async Task<AnalyticsSummaryResponse> GetSummaryAsync(
        Guid userId,
        DateTime from,
        DateTime to,
        Guid? clientId,
        CancellationToken ct = default
    )
    {
        var query = db
            .Sessions.Where(s =>
                s.UserId == userId
                && s.EndedAtUtc != null
                && s.StartedAtUtc >= from
                && s.StartedAtUtc < to
            );

        if (clientId.HasValue)
            query = query.Where(s => s.ClientId == clientId.Value);

        var sessions = await query.ToListAsync(ct);

        if (sessions.Count == 0)
        {
            return new AnalyticsSummaryResponse(
                new DateRange(from, to),
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0
            );
        }

        var totalFocused = sessions.Sum(s => s.FocusedSeconds ?? 0);
        var totalDistracted = sessions.Sum(s => s.DistractedSeconds ?? 0);
        var avgScore =
            sessions.Where(s => s.FocusScorePercent.HasValue).Select(s => s.FocusScorePercent!.Value)
            is var scores && scores.Any()
                ? (int)scores.Average()
                : 0;
        var totalDistractions = sessions.Sum(s => s.DistractionCount ?? 0);
        var totalContextSwitches = sessions.Sum(s => s.ContextSwitchCount ?? 0);
        var durations = sessions.Select(s =>
            (long)(s.EndedAtUtc!.Value - s.StartedAtUtc).TotalSeconds - s.TotalPausedSeconds
        );
        var avgDuration = (long)durations.Average();
        var longestDuration = durations.Max();
        var totalActive = durations.Sum();
        var clientsActive = sessions
            .Where(s => s.ClientId.HasValue)
            .Select(s => s.ClientId!.Value)
            .Distinct()
            .Count();

        return new AnalyticsSummaryResponse(
            new DateRange(from, to),
            sessions.Count,
            totalFocused,
            totalDistracted,
            avgScore,
            totalDistractions,
            totalContextSwitches,
            avgDuration,
            longestDuration,
            clientsActive,
            totalActive
        );
    }

    public async Task<AnalyticsTrendsResponse> GetTrendsAsync(
        Guid userId,
        DateTime from,
        DateTime to,
        string granularity,
        Guid? clientId,
        CancellationToken ct = default
    )
    {
        var query = db
            .Sessions.Where(s =>
                s.UserId == userId
                && s.EndedAtUtc != null
                && s.StartedAtUtc >= from
                && s.StartedAtUtc < to
            );

        if (clientId.HasValue)
            query = query.Where(s => s.ClientId == clientId.Value);

        var sessions = await query
            .OrderBy(s => s.StartedAtUtc)
            .ToListAsync(ct);

        var grouped = granularity switch
        {
            "weekly" => sessions.GroupBy(s => GetWeekStart(s.StartedAtUtc)),
            "monthly" => sessions.GroupBy(s => new DateTime(s.StartedAtUtc.Year, s.StartedAtUtc.Month, 1)),
            _ => sessions.GroupBy(s => s.StartedAtUtc.Date),
        };

        var dataPoints = grouped
            .Select(g =>
            {
                var scored = g.Where(s => s.FocusScorePercent.HasValue).ToList();
                var avgFocus = scored.Count > 0 ? (int)scored.Average(s => s.FocusScorePercent!.Value) : 0;

                return new TrendDataPoint(
                    g.Key.ToString("yyyy-MM-dd"),
                    g.Count(),
                    g.Sum(s => s.FocusedSeconds ?? 0),
                    g.Sum(s => s.DistractedSeconds ?? 0),
                    avgFocus,
                    g.Sum(s => s.DistractionCount ?? 0)
                );
            })
            .ToList();

        return new AnalyticsTrendsResponse(granularity, dataPoints);
    }

    public async Task<AnalyticsClientsResponse> GetClientBreakdownAsync(
        Guid userId,
        DateTime from,
        DateTime to,
        CancellationToken ct = default
    )
    {
        var sessions = await db
            .Sessions.Where(s =>
                s.UserId == userId
                && s.EndedAtUtc != null
                && s.ClientId != null
                && s.StartedAtUtc >= from
                && s.StartedAtUtc < to
            )
            .ToListAsync(ct);

        var clients = await db
            .Clients.Where(c => c.UserId == userId)
            .ToListAsync(ct);

        var clientMap = clients.ToDictionary(c => c.Id);

        var grouped = sessions.GroupBy(s => s.ClientId!.Value);

        var result = grouped
            .Select(g =>
            {
                var client = clientMap.GetValueOrDefault(g.Key);
                var scored = g.Where(s => s.FocusScorePercent.HasValue).ToList();
                var avgFocus = scored.Count > 0 ? (int)scored.Average(s => s.FocusScorePercent!.Value) : 0;

                return new ClientAnalytics(
                    g.Key,
                    client?.ClientType.ToString() ?? "Unknown",
                    client?.Name ?? "Unknown client",
                    g.Count(),
                    g.Sum(s => s.FocusedSeconds ?? 0),
                    g.Sum(s => s.DistractedSeconds ?? 0),
                    avgFocus
                );
            })
            .ToList();

        return new AnalyticsClientsResponse(result);
    }

    private static DateTime GetWeekStart(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff).Date;
    }
}

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
        Guid? deviceId,
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

        if (deviceId.HasValue)
            query = query.Where(s => s.DeviceId == deviceId.Value);

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
        var devicesActive = sessions
            .Where(s => s.DeviceId.HasValue)
            .Select(s => s.DeviceId!.Value)
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
            devicesActive
        );
    }

    public async Task<AnalyticsTrendsResponse> GetTrendsAsync(
        Guid userId,
        DateTime from,
        DateTime to,
        string granularity,
        Guid? deviceId,
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

        if (deviceId.HasValue)
            query = query.Where(s => s.DeviceId == deviceId.Value);

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

    public async Task<AnalyticsDevicesResponse> GetDeviceBreakdownAsync(
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
                && s.DeviceId != null
                && s.StartedAtUtc >= from
                && s.StartedAtUtc < to
            )
            .ToListAsync(ct);

        var devices = await db
            .Devices.Where(d => d.UserId == userId)
            .ToListAsync(ct);

        var deviceMap = devices.ToDictionary(d => d.Id);

        var grouped = sessions.GroupBy(s => s.DeviceId!.Value);

        var result = grouped
            .Select(g =>
            {
                var device = deviceMap.GetValueOrDefault(g.Key);
                var scored = g.Where(s => s.FocusScorePercent.HasValue).ToList();
                var avgFocus = scored.Count > 0 ? (int)scored.Average(s => s.FocusScorePercent!.Value) : 0;

                return new DeviceAnalytics(
                    g.Key,
                    device?.DeviceType.ToString() ?? "Unknown",
                    device?.Name ?? "Unknown Device",
                    g.Count(),
                    g.Sum(s => s.FocusedSeconds ?? 0),
                    g.Sum(s => s.DistractedSeconds ?? 0),
                    avgFocus
                );
            })
            .ToList();

        return new AnalyticsDevicesResponse(result);
    }

    private static DateTime GetWeekStart(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff).Date;
    }
}

using FocusBot.Core.DTOs;
using FocusBot.Core.Entities;
using FocusBot.Core.Events;
using FocusBot.Core.Interfaces;
using FocusBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FocusBot.Infrastructure.Services;

public sealed class DailyAnalyticsService : IDailyAnalyticsService
{
    private readonly AppDbContext _context;

    private class TodayAccumulator
    {
        public DateOnly Date { get; set; }
        public long FocusedSeconds { get; set; }
        public long UnclearSeconds { get; set; }
        public long DistractedSeconds { get; set; }
        public long TotalTrackedSeconds { get; set; }
        public int DistractionCount { get; set; }
    }

    private TodayAccumulator? _accumulator;
    private string? _mostPopularDistractionApp;
    private long _longestFocusedSessionSeconds;

    public DailyAnalyticsService(AppDbContext context)
    {
        _context = context;
    }

    public async Task UpdateForTickAsync(
        DateTime sampleTimeUtc,
        FocusStatus status,
        CancellationToken cancellationToken = default)
    {
        var localDate = GetLocalDate(sampleTimeUtc);

        if (_accumulator is null || _accumulator.Date != localDate)
            await ReloadTodayFromDbAsync(cancellationToken).ConfigureAwait(false);

        if (_accumulator is null || _accumulator.Date != localDate)
            _accumulator = new TodayAccumulator { Date = localDate };

        _accumulator.TotalTrackedSeconds++;

        switch (status)
        {
            case FocusStatus.Focused:
                _accumulator.FocusedSeconds++;
                break;
            case FocusStatus.Neutral:
                _accumulator.UnclearSeconds++;
                break;
            case FocusStatus.Distracted:
                _accumulator.DistractedSeconds++;
                break;
        }
    }

    public async Task RegisterDistractionEventAsync(
        DistractionEvent distractionEvent,
        CancellationToken cancellationToken = default)
    {
        var localDate = GetLocalDate(distractionEvent.OccurredAtUtc);

        if (_accumulator is null || _accumulator.Date != localDate)
            await ReloadTodayFromDbAsync(cancellationToken).ConfigureAwait(false);

        if (_accumulator is null || _accumulator.Date != localDate)
            _accumulator = new TodayAccumulator { Date = localDate };

        _accumulator.DistractionCount++;
    }

    public async Task<DailyFocusSummary?> GetTodaySummaryAsync(
        DateTime nowLocal,
        CancellationToken cancellationToken = default)
    {
        var localDate = DateOnly.FromDateTime(nowLocal);

        if (_accumulator is null || _accumulator.Date != localDate)
            await ReloadTodayFromDbAsync(cancellationToken).ConfigureAwait(false);

        if (_accumulator is null || _accumulator.Date != localDate)
            return null;

        return BuildSummaryFromAccumulator(localDate, _accumulator);
    }

    public async Task ReloadTodayFromDbAsync(CancellationToken cancellationToken = default)
    {
        var localDate = DateOnly.FromDateTime(DateTime.Now);

        var segments = await _context.FocusSegments
            .AsNoTracking()
            .Where(s => s.AnalyticsDateLocal == localDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var focusedSeconds = segments
            .Where(s => s.AlignmentScore >= 6)
            .Sum(s => (long)s.DurationSeconds);

        var unclearSeconds = segments
            .Where(s => s.AlignmentScore >= 4 && s.AlignmentScore < 6)
            .Sum(s => (long)s.DurationSeconds);

        var distractedSeconds = segments
            .Where(s => s.AlignmentScore < 4)
            .Sum(s => (long)s.DurationSeconds);

        var totalSeconds = focusedSeconds + unclearSeconds + distractedSeconds;

        var today = DateTime.Now;
        var startOfDayUtc = localDate.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        var endOfDayUtc = localDate.AddDays(1).ToDateTime(TimeOnly.MinValue).ToUniversalTime();

        var distractionEvents = await _context.DistractionEvents
            .AsNoTracking()
            .Where(e => e.OccurredAtUtc >= startOfDayUtc && e.OccurredAtUtc < endOfDayUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var distractionCount = distractionEvents.Count;

        var mostPopularDistractionApp = distractionEvents
            .GroupBy(e => e.ProcessName)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()
            ?.Key;

        var longestFocusedSession = segments
            .Where(s => s.AlignmentScore >= 6)
            .Max(s => (long?)s.DurationSeconds);

        _accumulator = new TodayAccumulator
        {
            Date = localDate,
            FocusedSeconds = focusedSeconds,
            UnclearSeconds = unclearSeconds,
            DistractedSeconds = distractedSeconds,
            TotalTrackedSeconds = totalSeconds,
            DistractionCount = distractionCount,
        };

        _mostPopularDistractionApp = mostPopularDistractionApp;
        _longestFocusedSessionSeconds = longestFocusedSession ?? 0;
    }

    private static DateOnly GetLocalDate(DateTime utcTimestamp)
    {
        var local = utcTimestamp.Kind == DateTimeKind.Local
            ? utcTimestamp
            : utcTimestamp.ToLocalTime();

        return DateOnly.FromDateTime(local);
    }

    private DailyFocusSummary? BuildSummaryFromAccumulator(DateOnly localDate, TodayAccumulator acc)
    {
        if (acc.TotalTrackedSeconds <= 0)
            return null;

        var totalSeconds = acc.FocusedSeconds + acc.UnclearSeconds + acc.DistractedSeconds;
        if (totalSeconds <= 0)
            return null;

        var focusRatio = (double)acc.FocusedSeconds / totalSeconds;
        var focusScoreBucket = (int)Math.Round(focusRatio * 10, MidpointRounding.AwayFromZero);
        focusScoreBucket = Math.Clamp(focusScoreBucket, 0, 10);

        TimeSpan? averageDistraction = null;
        if (acc.DistractionCount > 0 && acc.DistractedSeconds > 0)
        {
            var averageSeconds = (double)acc.DistractedSeconds / acc.DistractionCount;
            averageDistraction = TimeSpan.FromSeconds(averageSeconds);
        }

        TimeSpan? longestFocused = _longestFocusedSessionSeconds > 0
            ? TimeSpan.FromSeconds(_longestFocusedSessionSeconds)
            : null;

        return new DailyFocusSummary
        {
            AnalyticsDateLocal = localDate,
            FocusScoreBucket = focusScoreBucket,
            FocusedTime = TimeSpan.FromSeconds(acc.FocusedSeconds),
            DistractedTime = TimeSpan.FromSeconds(acc.DistractedSeconds),
            DistractionCount = acc.DistractionCount,
            AverageDistractionDuration = averageDistraction,
            MostPopularDistractionApp = _mostPopularDistractionApp,
            LongestFocusedSession = longestFocused
        };
    }
}

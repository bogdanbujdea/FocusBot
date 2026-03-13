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
        var entity = await GetOrCreateAsync(localDate, cancellationToken).ConfigureAwait(false);

        entity.TotalTrackedSeconds++;

        switch (status)
        {
            case FocusStatus.Focused:
                entity.FocusedSeconds++;
                break;
            case FocusStatus.Neutral:
                entity.UnclearSeconds++;
                break;
            case FocusStatus.Distracted:
                entity.DistractedSeconds++;
                break;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RegisterDistractionEventAsync(
        DistractionEvent distractionEvent,
        CancellationToken cancellationToken = default)
    {
        var localDate = GetLocalDate(distractionEvent.OccurredAtUtc);
        var entity = await GetOrCreateAsync(localDate, cancellationToken).ConfigureAwait(false);

        entity.DistractionCount++;

        if (distractionEvent.DistractedDurationSecondsAtEmit > 0)
        {
            var previousTotal = entity.DistractedSeconds;
            entity.DistractedSeconds = Math.Max(
                entity.DistractedSeconds,
                previousTotal + distractionEvent.DistractedDurationSecondsAtEmit);
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<DailyFocusSummary?> GetTodaySummaryAsync(
        DateTime nowLocal,
        CancellationToken cancellationToken = default)
    {
        var localDate = DateOnly.FromDateTime(nowLocal);

        var entity = await _context.DailyFocusAnalytics
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.AnalyticsDateLocal == localDate, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null || entity.TotalTrackedSeconds <= 0)
            return null;

        var focusRatio = entity.TotalTrackedSeconds == 0
            ? 0d
            : (double)entity.FocusedSeconds / entity.TotalTrackedSeconds;

        var focusScoreBucket = (int)Math.Round(focusRatio * 10, MidpointRounding.AwayFromZero);
        focusScoreBucket = Math.Clamp(focusScoreBucket, 0, 10);

        TimeSpan? averageDistraction = null;
        if (entity.DistractionCount > 0 && entity.DistractedSeconds > 0)
        {
            var averageSeconds = (double)entity.DistractedSeconds / entity.DistractionCount;
            averageDistraction = TimeSpan.FromSeconds(averageSeconds);
        }

        return new DailyFocusSummary
        {
            AnalyticsDateLocal = entity.AnalyticsDateLocal,
            FocusScoreBucket = focusScoreBucket,
            FocusedTime = TimeSpan.FromSeconds(entity.FocusedSeconds),
            DistractedTime = TimeSpan.FromSeconds(entity.DistractedSeconds),
            DistractionCount = entity.DistractionCount,
            AverageDistractionDuration = averageDistraction
        };
    }

    private static DateOnly GetLocalDate(DateTime utcTimestamp)
    {
        var local = utcTimestamp.Kind == DateTimeKind.Local
            ? utcTimestamp
            : utcTimestamp.ToLocalTime();

        return DateOnly.FromDateTime(local);
    }

    private async Task<DailyFocusAnalytics> GetOrCreateAsync(
        DateOnly localDate,
        CancellationToken cancellationToken)
    {
        var existing = await _context.DailyFocusAnalytics
            .SingleOrDefaultAsync(x => x.AnalyticsDateLocal == localDate, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
            return existing;

        var entity = new DailyFocusAnalytics
        {
            AnalyticsDateLocal = localDate
        };

        _context.DailyFocusAnalytics.Add(entity);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return entity;
    }
}


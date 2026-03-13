using FocusBot.Core.DTOs;
using FocusBot.Core.Entities;
using FocusBot.Core.Events;

namespace FocusBot.Core.Interfaces;

public interface IDailyAnalyticsService
{
    Task UpdateForTickAsync(
        DateTime sampleTimeUtc,
        FocusStatus status,
        CancellationToken cancellationToken = default);

    Task RegisterDistractionEventAsync(
        DistractionEvent distractionEvent,
        CancellationToken cancellationToken = default);

    Task<DailyFocusSummary?> GetTodaySummaryAsync(
        DateTime nowLocal,
        CancellationToken cancellationToken = default);

    Task ReloadTodayFromDbAsync(CancellationToken cancellationToken = default);
}


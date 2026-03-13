using FocusBot.Core.DTOs;

namespace FocusBot.Core.Interfaces;

public interface IDistractionAnalyticsService
{
    Task<SessionDistractionSummary> GetSessionSummaryAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<int> GetLiveDistractionCountAsync(Guid sessionId, CancellationToken cancellationToken = default);
}


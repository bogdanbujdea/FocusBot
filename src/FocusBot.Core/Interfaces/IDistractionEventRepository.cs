using FocusBot.Core.Entities;

namespace FocusBot.Core.Interfaces;

public interface IDistractionEventRepository
{
    Task AddAsync(DistractionEvent distractionEvent, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DistractionEvent>> GetEventsForTaskBetweenAsync(
        string taskId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    Task DeleteDistractionEventsForTaskAsync(string taskId, CancellationToken cancellationToken = default);
}


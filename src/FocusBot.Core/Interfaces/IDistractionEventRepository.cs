using FocusBot.Core.Entities;

namespace FocusBot.Core.Interfaces;

public interface IDistractionEventRepository
{
    Task AddAsync(DistractionEvent distractionEvent, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DistractionEvent>> GetEventsForTaskAsync(
        string taskId,
        CancellationToken cancellationToken = default
    );

    Task DeleteDistractionEventsForTaskAsync(
        string taskId,
        CancellationToken cancellationToken = default
    );
}

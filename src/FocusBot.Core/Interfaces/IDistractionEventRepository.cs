using FocusBot.Core.Entities;

namespace FocusBot.Core.Interfaces;

public interface IDistractionEventRepository
{
    Task AddAsync(DistractionEvent distractionEvent, CancellationToken cancellationToken = default);
}


using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using FocusBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FocusBot.Infrastructure.Repositories;

public class DistractionEventRepository(AppDbContext context) : IDistractionEventRepository
{
    public async Task AddAsync(DistractionEvent distractionEvent, CancellationToken cancellationToken = default)
    {
        await context.DistractionEvents.AddAsync(distractionEvent, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}


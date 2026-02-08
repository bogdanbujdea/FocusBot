using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FocusBot.Infrastructure.Data;

public class AlignmentCacheRepository(AppDbContext context) : IAlignmentCacheRepository
{
    public async Task<AlignmentCacheEntry?> GetAsync(string contextHash, string taskContentHash)
    {
        return await context.AlignmentCacheEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ContextHash == contextHash && e.TaskContentHash == taskContentHash);
    }

    public async Task SaveAsync(WindowContext windowContext, AlignmentCacheEntry entry)
    {
        var existingContext = await context.WindowContexts.FindAsync(windowContext.ContextHash);
        if (existingContext == null)
            context.WindowContexts.Add(windowContext);

        var existingEntry = await context.AlignmentCacheEntries
            .FirstOrDefaultAsync(e => e.ContextHash == entry.ContextHash && e.TaskContentHash == entry.TaskContentHash);
        if (existingEntry != null)
        {
            existingEntry.Score = entry.Score;
            existingEntry.Reason = entry.Reason;
        }
        else
        {
            context.AlignmentCacheEntries.Add(entry);
        }

        await context.SaveChangesAsync();
    }

    public async Task<int> DeleteEntriesOlderThanAsync(TimeSpan age)
    {
        var cutoff = DateTime.UtcNow - age;
        var toDelete = await context.AlignmentCacheEntries
            .Where(e => e.CreatedAt < cutoff)
            .ToListAsync();
        context.AlignmentCacheEntries.RemoveRange(toDelete);
        await context.SaveChangesAsync();
        return toDelete.Count;
    }
}

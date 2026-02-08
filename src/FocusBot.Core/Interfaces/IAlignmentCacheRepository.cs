using FocusBot.Core.Entities;

namespace FocusBot.Core.Interfaces;

/// <summary>
/// Repository for alignment classification cache (window context + task content hash to score/reason).
/// </summary>
public interface IAlignmentCacheRepository
{
    Task<AlignmentCacheEntry?> GetAsync(string contextHash, string taskContentHash);

    Task SaveAsync(WindowContext context, AlignmentCacheEntry entry);

    Task<int> DeleteEntriesOlderThanAsync(TimeSpan age);
}

namespace FocusBot.Core.Entities;

/// <summary>
/// Cached AI alignment classification result for a (window context, task content) pair.
/// </summary>
public class AlignmentCacheEntry
{
    public string ContextHash { get; set; } = string.Empty;
    public string TaskContentHash { get; set; } = string.Empty;
    public int Score { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

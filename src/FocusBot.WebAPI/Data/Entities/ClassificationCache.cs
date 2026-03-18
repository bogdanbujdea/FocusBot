namespace FocusBot.WebAPI.Data.Entities;

/// <summary>
/// Cached LLM classification result keyed by user, context hash, and task content hash.
/// Entries expire after 24 hours.
/// </summary>
public class ClassificationCache
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string ContextHash { get; set; } = string.Empty;
    public string TaskContentHash { get; set; } = string.Empty;
    public int Score { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public User User { get; set; } = null!;
}

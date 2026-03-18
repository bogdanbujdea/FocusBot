namespace FocusBot.WebAPI.Data.Entities;

/// <summary>
/// Represents a focus session started by a user. Only one active (un-ended) session per user is allowed.
/// </summary>
public class Session
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TaskText { get; set; } = string.Empty;
    public string? TaskHints { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }
    public int? FocusScorePercent { get; set; }
    public long? FocusedSeconds { get; set; }
    public long? DistractedSeconds { get; set; }
    public int? DistractionCount { get; set; }
    public int? ContextSwitchCostSeconds { get; set; }
    public string? TopDistractingApps { get; set; }
    public string Source { get; set; } = "api";

    public User User { get; set; } = null!;
}

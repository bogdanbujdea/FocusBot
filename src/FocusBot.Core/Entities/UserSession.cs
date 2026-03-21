namespace FocusBot.Core.Entities;

/// <summary>
/// Represents a user-defined task (single-task flow: active or completed).
/// </summary>
public class UserSession
{
    public UserSession() { }

    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public string SessionTitle { get; set; } = string.Empty;
    public string? Context { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long TotalElapsedSeconds { get; set; } = 0;
    public int? FocusScorePercent { get; set; }

    public long FocusedSeconds { get; set; }
    public long DistractedSeconds { get; set; }
    public int DistractionCount { get; set; }
    public int ContextSwitchCount { get; set; }
    public string? TopDistractingApps { get; set; }
    public string? TopAlignedApps { get; set; }

    public bool IsActive => !IsCompleted;
}

namespace FocusBot.Core.Entities;

/// <summary>
/// Represents a user-defined task (single-task flow: active or completed).
/// </summary>
public class UserTask
{
    public UserTask() { }

    public string TaskId { get; set; } = Guid.NewGuid().ToString();
    public string Description { get; set; } = string.Empty;
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

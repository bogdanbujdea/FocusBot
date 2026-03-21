namespace FocusBot.WebAPI.Data.Entities;

/// <summary>
/// Represents a focus session started by a user. Only one active (un-ended) session per user is allowed.
/// </summary>
public class Session
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    /// <summary>The device that started this session. Null for sessions created before device registration was available.</summary>
    public Guid? DeviceId { get; set; }

    public string TaskText { get; set; } = string.Empty;
    public string? TaskHints { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }
    public int? FocusScorePercent { get; set; }
    public long? FocusedSeconds { get; set; }
    public long? DistractedSeconds { get; set; }
    public int? DistractionCount { get; set; }
    public int? ContextSwitchCount { get; set; }

    /// <summary>JSON-serialised list of top distracting apps with time spent (computed client-side).</summary>
    public string? TopDistractingApps { get; set; }

    /// <summary>JSON-serialised list of top aligned apps with time spent (computed client-side).</summary>
    public string? TopAlignedApps { get; set; }

    public string Source { get; set; } = "api";

    public User User { get; set; } = null!;
}

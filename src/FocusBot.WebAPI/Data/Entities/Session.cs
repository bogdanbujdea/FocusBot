namespace FocusBot.WebAPI.Data.Entities;

/// <summary>
/// Represents a focus session started by a user. Only one active (un-ended) session per user is allowed.
/// </summary>
public class Session
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    /// <summary>The client that started this session. Null for sessions created before client registration was available.</summary>
    public Guid? ClientId { get; set; }

    public string SessionTitle { get; set; } = string.Empty;
    public string? Context { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }

    /// <summary>When the session was most recently paused. Null if session is not currently paused.</summary>
    public DateTime? PausedAtUtc { get; set; }

    /// <summary>Total accumulated pause duration in seconds across all pause/resume cycles.</summary>
    public long TotalPausedSeconds { get; set; }

    /// <summary>True if the session is currently paused (active but not running).</summary>
    public bool IsPaused => PausedAtUtc.HasValue && !EndedAtUtc.HasValue;

    public int? FocusScorePercent { get; set; }
    public long? FocusedSeconds { get; set; }
    public long? DistractedSeconds { get; set; }
    public int? DistractionCount { get; set; }
    public int? ContextSwitchCount { get; set; }

    public string Source { get; set; } = "api";

    public User User { get; set; } = null!;
}

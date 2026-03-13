namespace FocusBot.Core.Entities;

/// <summary>
/// Represents a single distracted episode during a deep work session.
/// One event is emitted per continuous distracted run that crosses the threshold.
/// </summary>
public class DistractionEvent
{
    public int Id { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public string TaskId { get; set; } = string.Empty;

    public Guid? SessionId { get; set; }

    public string ProcessName { get; set; } = string.Empty;

    public string? WindowTitleSnapshot { get; set; }

    public int DistractedDurationSecondsAtEmit { get; set; }
}


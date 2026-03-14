namespace FocusBot.Core.Entities;

/// <summary>
/// Aggregated focus segment per (TaskId, ContextHash, AlignmentScore, AnalyticsDateLocal).
/// One row per unique context+score+date combination; duration accumulates across visits within that day.
/// </summary>
public class FocusSegment
{
    public int Id { get; set; }
    public string TaskId { get; set; } = string.Empty;
    public string ContextHash { get; set; } = string.Empty;
    public int AlignmentScore { get; set; }
    public int DurationSeconds { get; set; }
    public string? WindowTitle { get; set; }
    public string? ProcessName { get; set; }
    public DateOnly AnalyticsDateLocal { get; set; }
}

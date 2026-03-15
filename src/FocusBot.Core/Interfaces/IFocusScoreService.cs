namespace FocusBot.Core.Interfaces;

/// <summary>
/// Tracks focus segments per task and calculates focus score (0-100%).
/// Aggregates by (TaskId, ContextHash, AlignmentScore).
/// </summary>
public interface IFocusScoreService
{
    void StartOrResumeSegment(string taskId, string contextHash, int alignmentScore,
        string? windowTitle, string? processName);
    void StartPendingSegment(string taskId, string contextHash,
        string? windowTitle, string? processName);
    void UpdatePendingSegmentScore(int alignmentScore);
    /// <summary>
    /// Pauses the current segment and adds its duration to the segment total.
    /// When <paramref name="backdateBy"/> is set (e.g. idle threshold), the duration added is reduced by that amount to avoid over-counting idle time.
    /// </summary>
    void PauseCurrentSegment(TimeSpan? backdateBy = null);
    int CalculateFocusScorePercent(string taskId);
    int GetCurrentSegmentDurationSeconds();
    bool HasRealScore { get; }
    Task PersistSegmentsAsync();
    Task LoadSegmentsForTaskAsync(string taskId);
    void ClearTaskSegments(string taskId);
    
    /// <summary>
    /// Updates all historical focus segments for a given task+contextHash to a new alignment score.
    /// Used when user manually overrides the classification for a window.
    /// </summary>
    Task UpdateHistoricalSegmentsAsync(string taskId, string contextHash, int newAlignmentScore);
}

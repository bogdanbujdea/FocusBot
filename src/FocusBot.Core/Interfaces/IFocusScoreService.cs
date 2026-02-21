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
    void PauseCurrentSegment();
    int CalculateFocusScorePercent(string taskId);
    int GetCurrentSegmentDurationSeconds();
    bool HasRealScore { get; }
    Task PersistSegmentsAsync();
    Task LoadSegmentsForTaskAsync(string taskId);
    void ClearTaskSegments(string taskId);
}

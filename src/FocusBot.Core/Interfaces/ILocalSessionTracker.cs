using FocusBot.Core.Entities;

namespace FocusBot.Core.Interfaces;

/// <summary>
/// Tracks focus session state locally: time accounting, focus score, distraction detection,
/// and per-app analytics. Replaces the deleted FocusScoreService, DistractionDetectorService,
/// TaskSummaryService, and DailyAnalyticsService.
/// </summary>
public interface ILocalSessionTracker
{
    /// <summary>Starts a new tracking session, resetting all counters.</summary>
    void Start(string taskText);

    /// <summary>
    /// Records one classification tick for the given process.
    /// Called every second by the focus monitoring loop.
    /// </summary>
    void RecordClassification(string processName, AlignmentResult result);

    /// <summary>
    /// Notifies the tracker that the user is idle (tracking paused) or active again.
    /// </summary>
    void HandleIdle(bool isIdle);

    /// <summary>Returns the current focus score (0–100) based on accumulated time.</summary>
    int GetFocusScore();

    /// <summary>Computes and returns the complete session summary for backend submission.</summary>
    SessionSummary GetSessionSummary();

    /// <summary>Resets all state. Called when the session ends.</summary>
    void Reset();
}

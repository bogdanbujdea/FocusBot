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
    void Start(string sessionTitle);

    /// <summary>
    /// Records a new classification result for the given process.
    /// Stores the alignment state and handles transition detection (distraction/context-switch counts).
    /// Does NOT increment time counters — that is done by <see cref="RecordTick"/>.
    /// </summary>
    void RecordClassification(string processName, AlignmentResult result);

    /// <summary>
    /// Accumulates one second of focused or distracted time based on the last known alignment state.
    /// Called once per second by the monitoring timer. No-ops if no classification has been recorded yet or if idle.
    /// </summary>
    void RecordTick();

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

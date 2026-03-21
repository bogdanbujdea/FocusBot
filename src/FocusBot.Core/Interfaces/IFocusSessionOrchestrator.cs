using FocusBot.Core.Entities;
using FocusBot.Core.Events;

namespace FocusBot.Core.Interfaces;

/// <summary>
/// Orchestrates focus session business logic: time tracking, classification triggers,
/// idle/active handling, and backend synchronization. The ViewModel subscribes to
/// <see cref="StateChanged"/> and updates UI-bound properties.
/// </summary>
public interface IFocusSessionOrchestrator
{
    /// <summary>
    /// Raised when session state changes (elapsed time, classification result, pause state, etc.).
    /// The ViewModel should update UI-bound properties from the event args.
    /// </summary>
    event EventHandler<FocusSessionStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Starts orchestrating a new focus session. Subscribes to window monitor events
    /// and begins time tracking.
    /// </summary>
    /// <param name="session">The active session to track.</param>
    /// <param name="initialElapsedSeconds">Initial elapsed seconds (for resuming a session).</param>
    void StartSession(UserSession session, long initialElapsedSeconds = 0);

    /// <summary>
    /// Ends the current session. Stops monitoring, computes final summary,
    /// and notifies the backend.
    /// </summary>
    /// <returns>The session summary with focus metrics, or null if no active session.</returns>
    Task<SessionSummary?> EndSessionAsync();

    /// <summary>
    /// Pauses the current session. Stops time tracking and monitoring.
    /// </summary>
    void PauseSession();

    /// <summary>
    /// Resumes a paused session. Restarts time tracking and monitoring.
    /// </summary>
    void ResumeSession();

    /// <summary>
    /// Synchronizes backend session state on startup. If an active session exists
    /// on the backend, adopts it so EndSession can properly close it.
    /// </summary>
    Task SyncBackendSessionAsync();

    /// <summary>
    /// Records a manual focus override (user marks current window as focused/distracting).
    /// </summary>
    /// <param name="newScore">The override score (e.g., 9 for focused, 2 for distracting).</param>
    /// <param name="newReason">The override reason text.</param>
    Task RecordManualOverrideAsync(int newScore, string newReason);

    /// <summary>
    /// Whether there is currently an active session being tracked.
    /// </summary>
    bool HasActiveSession { get; }

    /// <summary>
    /// Whether the current session is paused.
    /// </summary>
    bool IsSessionPaused { get; }

    /// <summary>
    /// Current session elapsed seconds.
    /// </summary>
    long SessionElapsedSeconds { get; }

    /// <summary>
    /// Current focus score percentage (0-100).
    /// </summary>
    int FocusScorePercent { get; }
}

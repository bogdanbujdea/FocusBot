using FocusBot.Core.Entities;
using FocusBot.Core.Events;

namespace FocusBot.Core.Interfaces;

/// <summary>
/// Orchestrates focus session business logic: time tracking, classification triggers,
/// idle/active handling, and Web API session lifecycle. The ViewModel subscribes to
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
    /// Starts a session on the server, then begins local monitoring when successful.
    /// </summary>
    Task<ApiResult<UserSession>> StartSessionAsync(string sessionTitle, string? sessionContext);

    /// <summary>
    /// Resumes local monitoring for a session already returned by the API (e.g. after app restart).
    /// Does not call the API.
    /// </summary>
    void BeginLocalSessionTracking(UserSession session, long initialElapsedSeconds = 0);

    /// <summary>
    /// Returns the current active session from the API, or null if none.
    /// </summary>
    Task<UserSession?> LoadActiveSessionAsync();

    /// <summary>
    /// Ends the current session. Calls the API first; on failure the local session stays active.
    /// </summary>
    /// <returns>Summary and optional API error message, or null if no active session.</returns>
    Task<SessionEndResult?> EndSessionAsync();

    /// <summary>
    /// Pauses the current session. Stops time tracking and monitoring.
    /// </summary>
    void PauseSession();

    /// <summary>
    /// Resumes a paused session. Restarts time tracking and monitoring.
    /// </summary>
    void ResumeSession();

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

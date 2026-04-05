using FocusBot.Core.Entities;

namespace FocusBot.Core.Interfaces;

/// <summary>
/// Centralized coordinator for session lifecycle state and operations.
/// Acts as single source of truth for active session state.
/// </summary>
public interface ISessionCoordinator
{
    /// <summary>
    /// Current session state snapshot.
    /// </summary>
    SessionState CurrentState { get; }

    /// <summary>
    /// Event raised when session state changes.
    /// Provides full state snapshot and change type metadata.
    /// </summary>
    event Action<SessionState, SessionChangeType>? StateChanged;

    /// <summary>
    /// Initialize coordinator and load any existing active session from API.
    /// Should be called once after authentication is established.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Start a new session with the given title and optional context.
    /// Returns true if successful, false if failed (check CurrentState.ErrorMessage).
    /// </summary>
    Task<bool> StartAsync(string title, string? context);

    /// <summary>
    /// Pause the currently active session.
    /// Returns true if successful, false if failed or no active session.
    /// </summary>
    Task<bool> PauseAsync();

    /// <summary>
    /// Resume the currently paused session.
    /// Returns true if successful, false if failed or session not paused.
    /// </summary>
    Task<bool> ResumeAsync();

    /// <summary>
    /// Stop the currently active session with placeholder metrics.
    /// Returns true if successful, false if failed or no active session.
    /// </summary>
    Task<bool> StopAsync();

    /// <summary>
    /// Apply a remote session started event from realtime transport (SignalR).
    /// </summary>
    Task ApplyRemoteSessionStartedAsync(SessionStartedEvent evt);

    /// <summary>
    /// Clear any error state.
    /// </summary>
    void ClearError();

    /// <summary>
    /// Reset coordinator state (e.g., on sign-out).
    /// Clears active session and returns to initial state.
    /// </summary>
    void Reset();
}

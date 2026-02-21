namespace FocusBot.Core.Interfaces;

/// <summary>
/// Service that detects when the user becomes idle (no keyboard/mouse input)
/// and raises events for idle/active state transitions.
/// </summary>
public interface IIdleDetectionService
{
    /// <summary>
    /// Raised when the user becomes idle (no input for longer than IdleThreshold).
    /// </summary>
    event EventHandler? UserBecameIdle;

    /// <summary>
    /// Raised when the user becomes active again after being idle.
    /// </summary>
    event EventHandler? UserBecameActive;

    /// <summary>
    /// The duration of inactivity before the user is considered idle.
    /// Default is 5 minutes.
    /// </summary>
    TimeSpan IdleThreshold { get; set; }

    /// <summary>
    /// Whether the user is currently considered idle.
    /// </summary>
    bool IsUserIdle { get; }

    /// <summary>
    /// Start monitoring for idle state.
    /// </summary>
    void Start();

    /// <summary>
    /// Stop monitoring for idle state.
    /// </summary>
    void Stop();
}

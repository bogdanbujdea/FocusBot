using FocusBot.Core.Events;

namespace FocusBot.Core.Interfaces;

/// <summary>
/// Service that monitors foreground window changes, tracks elapsed time, and detects idle state.
/// Consolidates window monitoring, time tracking, and idle detection into a single polling service.
/// Start when a task is in progress; stop when none.
/// </summary>
public interface IWindowMonitorService
{
    /// <summary>
    /// Start monitoring. Captures the current SynchronizationContext for event marshalling.
    /// </summary>
    void Start();

    /// <summary>
    /// Stop monitoring and reset state.
    /// </summary>
    void Stop();

    /// <summary>
    /// Raised when the foreground window changes.
    /// </summary>
    event EventHandler<ForegroundWindowChangedEventArgs>? ForegroundWindowChanged;

    /// <summary>
    /// Raised every second while monitoring (for elapsed time tracking).
    /// </summary>
    event EventHandler? Tick;

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
}

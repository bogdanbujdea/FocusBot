namespace FocusBot.Core.Interfaces;

/// <summary>
/// Service that fires a tick event every second for time tracking displays.
/// Start when a task is in progress; stop when none.
/// </summary>
public interface ITimeTrackingService
{
    /// <summary>
    /// Fires every second when the service is running.
    /// </summary>
    event EventHandler? Tick;

    void Start();
    void Stop();
    bool IsRunning { get; }
}

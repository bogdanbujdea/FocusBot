using FocusBot.Core.Events;

namespace FocusBot.Core.Interfaces;

/// <summary>
/// Service that monitors the foreground window and raises events when it changes.
/// Start when a task is in progress; stop when none.
/// </summary>
public interface IWindowMonitorService
{
    void Start();
    void Stop();
    event EventHandler<ForegroundWindowChangedEventArgs>? ForegroundWindowChanged;
}

namespace FocusBot.Core.Events;

/// <summary>
/// Event args for foreground window change notifications from the window monitor.
/// </summary>
public class ForegroundWindowChangedEventArgs : EventArgs
{
    public required string ProcessName { get; init; }
    public required string WindowTitle { get; init; }
}

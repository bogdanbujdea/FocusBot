namespace FocusBot.Core.Events;

/// <summary>
/// Represents the current focus status for the overlay display.
/// </summary>
public enum FocusStatus
{
    /// <summary>Distracted - alignment score less than 4.</summary>
    Distracted,

    /// <summary>Neutral - alignment score 4-5.</summary>
    Neutral,

    /// <summary>Focused - alignment score 6 or higher.</summary>
    Focused
}

/// <summary>
/// Event args for focus overlay state changes (score, status, active task).
/// </summary>
public class FocusOverlayStateChangedEventArgs : EventArgs
{
    /// <summary>Whether there is currently an active (in-progress) task.</summary>
    public required bool HasActiveTask { get; init; }

    /// <summary>The current focus score percentage (0-100). Only meaningful when HasActiveTask is true.</summary>
    public required int FocusScorePercent { get; init; }

    /// <summary>The current focus status (Distracted/Neutral/Focused). Only meaningful when HasActiveTask is true.</summary>
    public required FocusStatus Status { get; init; }

    /// <summary>Whether the task is currently paused.</summary>
    public bool IsTaskPaused { get; init; }
}

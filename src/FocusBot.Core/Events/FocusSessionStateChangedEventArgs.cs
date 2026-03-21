namespace FocusBot.Core.Events;

/// <summary>
/// Event args raised when focus session state changes (elapsed time, classification result, etc.).
/// </summary>
public class FocusSessionStateChangedEventArgs : EventArgs
{
    /// <summary>Total session elapsed seconds.</summary>
    public required long SessionElapsedSeconds { get; init; }

    /// <summary>Current focus score percentage (0-100).</summary>
    public required int FocusScorePercent { get; init; }

    /// <summary>Whether a classification is currently in progress.</summary>
    public required bool IsClassifying { get; init; }

    /// <summary>Latest alignment score (0-10).</summary>
    public required int FocusScore { get; init; }

    /// <summary>Latest alignment reason text.</summary>
    public required string FocusReason { get; init; }

    /// <summary>Whether a classification result has been received for the current window.</summary>
    public required bool HasCurrentFocusResult { get; init; }

    /// <summary>Whether the session is paused.</summary>
    public required bool IsSessionPaused { get; init; }

    /// <summary>Error message from the last classification attempt, or null if no error.</summary>
    public string? AiRequestError { get; init; }

    /// <summary>Current foreground process name.</summary>
    public required string CurrentProcessName { get; init; }

    /// <summary>Current foreground window title.</summary>
    public required string CurrentWindowTitle { get; init; }
}

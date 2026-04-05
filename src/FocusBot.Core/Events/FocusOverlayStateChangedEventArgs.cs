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
    Focused,
}

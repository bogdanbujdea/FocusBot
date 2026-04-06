namespace FocusBot.Core.Entities;

/// <summary>
/// Classification result with a user-friendly label and source for display.
/// Source is "desktop" for local foreground classifications, "extension" for browser-extension classifications.
/// </summary>
public sealed record ClassificationStatus(
    int Score,
    string Reason,
    string Label,
    string Source
)
{
    /// <summary>
    /// Maps a score (1-10) to a classification status.
    /// </summary>
    public static ClassificationStatus FromScore(int score, string reason, string source = "desktop") => new(
        score,
        reason,
        score > 5 ? "Focused" : score < 5 ? "Distracted" : "Neutral",
        source
    );
}

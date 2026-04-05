namespace FocusBot.Core.Entities;

/// <summary>
/// Classification result with a user-friendly label for display.
/// </summary>
public sealed record ClassificationStatus(
    int Score,
    string Reason,
    string Label
)
{
    /// <summary>
    /// Maps a score (1-10) to a classification status.
    /// </summary>
    public static ClassificationStatus FromScore(int score, string reason) => new(
        score,
        reason,
        score > 5 ? "Focused" : score < 5 ? "Distracted" : "Neutral"
    );
}

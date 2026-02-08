namespace FocusBot.Core.Entities;

/// <summary>
/// Result of classifying how aligned the current window is with the user's task.
/// </summary>
public class AlignmentResult
{
    public int Score { get; init; }
    public string Reason { get; init; } = string.Empty;
}

namespace FocusBot.Core.Helpers;

/// <summary>
/// Provides focus score calculation utilities.
/// </summary>
public static class FocusScoreHelper
{
    /// <summary>
    /// Computes the focus score percentage from aligned and misaligned time.
    /// </summary>
    /// <param name="focusedSeconds">Time spent in focused/aligned state.</param>
    /// <param name="distractedSeconds">Time spent in distracted/misaligned state.</param>
    /// <returns>Focus score as a percentage (0-100), or 0 if total time is 0.</returns>
    public static int ComputeFocusScorePercentage(long focusedSeconds, long distractedSeconds)
    {
        var total = focusedSeconds + distractedSeconds;
        if (total == 0)
            return 0;

        return (int)Math.Round((double)focusedSeconds / total * 100);
    }
}

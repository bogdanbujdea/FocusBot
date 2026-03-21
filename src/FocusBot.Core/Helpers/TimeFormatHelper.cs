namespace FocusBot.Core.Helpers;

/// <summary>
/// Provides consistent time formatting utilities across the application.
/// </summary>
public static class TimeFormatHelper
{
    /// <summary>
    /// Formats seconds as HH:mm:ss for precise elapsed time display.
    /// </summary>
    /// <param name="totalSeconds">Total elapsed seconds.</param>
    /// <returns>Formatted time string (e.g., "01:23:45").</returns>
    public static string FormatElapsed(long totalSeconds)
    {
        var hours = (int)(totalSeconds / 3600);
        var minutes = (int)((totalSeconds % 3600) / 60);
        var seconds = (int)(totalSeconds % 60);
        return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
    }

    /// <summary>
    /// Formats seconds as compact text: 45s, 5m 30s, 2h 15m.
    /// Used for summary displays and tooltips.
    /// </summary>
    /// <param name="totalSeconds">Total elapsed seconds.</param>
    /// <returns>Compact time string (e.g., "2h 15m", "5m 30s", "45s").</returns>
    public static string FormatTimeShort(long totalSeconds)
    {
        if (totalSeconds < 60)
            return $"{totalSeconds}s";

        var hours = (int)(totalSeconds / 3600);
        var minutes = (int)((totalSeconds % 3600) / 60);

        if (hours > 0)
            return $"{hours}h {minutes}m";

        return $"{minutes}m";
    }
}

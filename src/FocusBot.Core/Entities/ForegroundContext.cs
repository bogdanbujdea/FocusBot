namespace FocusBot.Core.Entities;

/// <summary>
/// Current foreground window/tab context being monitored.
/// </summary>
public sealed record ForegroundContext(
    string ProcessName,
    string WindowTitle,
    bool IsClassifying
)
{
    /// <summary>
    /// Returns a display-friendly label for the current context.
    /// For browsers, shows window title. For other apps, shows "ProcessName - WindowTitle".
    /// </summary>
    public string DisplayLabel
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ProcessName) && string.IsNullOrWhiteSpace(WindowTitle))
                return "Waiting for activity...";

            if (IsBrowser)
                return TruncateWindowTitle(WindowTitle);

            if (string.IsNullOrWhiteSpace(WindowTitle))
                return ProcessName;

            return $"{ProcessName} - {TruncateWindowTitle(WindowTitle)}";
        }
    }

    /// <summary>
    /// True if this is a known browser process.
    /// </summary>
    public bool IsBrowser => ProcessName.Equals("chrome", StringComparison.OrdinalIgnoreCase)
        || ProcessName.Equals("msedge", StringComparison.OrdinalIgnoreCase)
        || ProcessName.Equals("brave", StringComparison.OrdinalIgnoreCase)
        || ProcessName.Equals("firefox", StringComparison.OrdinalIgnoreCase)
        || ProcessName.Equals("opera", StringComparison.OrdinalIgnoreCase);

    private static string TruncateWindowTitle(string title)
    {
        const int maxLength = 60;
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;
        return title.Length <= maxLength ? title : title[..maxLength] + "...";
    }
}

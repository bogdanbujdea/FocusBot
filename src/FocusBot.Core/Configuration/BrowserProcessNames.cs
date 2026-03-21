namespace FocusBot.Core.Configuration;

/// <summary>
/// Known browser process names for extension integration and classification hints.
/// </summary>
public static class BrowserProcessNames
{
    /// <summary>
    /// All recognized browser process names (case-insensitive).
    /// </summary>
    public static readonly HashSet<string> AllBrowsers = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome",
        "msedge",
        "firefox",
        "brave",
        "opera",
        "vivaldi",
        "Google Chrome",
        "Microsoft Edge",
        "Firefox",
        "Brave Browser",
    };

    /// <summary>
    /// Browsers supported by the Foqus extension (Edge and Chrome only).
    /// </summary>
    public static readonly HashSet<string> ExtensionSupported = new(StringComparer.OrdinalIgnoreCase)
    {
        "msedge",
        "chrome",
        "Microsoft Edge",
        "Google Chrome",
    };

    /// <summary>
    /// Checks if the given process name is a known browser.
    /// </summary>
    /// <param name="processName">Process name to check.</param>
    /// <returns>True if the process is a recognized browser.</returns>
    public static bool IsBrowser(string processName) =>
        !string.IsNullOrEmpty(processName) && AllBrowsers.Contains(processName);

    /// <summary>
    /// Checks if the given process is a browser supported by the Foqus extension.
    /// </summary>
    /// <param name="processName">Process name to check.</param>
    /// <returns>True if the browser supports the Foqus extension (Edge or Chrome).</returns>
    public static bool IsExtensionSupported(string processName) =>
        !string.IsNullOrEmpty(processName) && ExtensionSupported.Contains(processName);
}

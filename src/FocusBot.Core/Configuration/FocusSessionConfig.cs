namespace FocusBot.Core.Configuration;

/// <summary>
/// Configuration constants for focus session behavior.
/// </summary>
public static class FocusSessionConfig
{
    /// <summary>
    /// Interval (in seconds) between persisting elapsed time to the database.
    /// This prevents data loss in case of crashes while avoiding excessive I/O.
    /// </summary>
    public const int PersistIntervalSeconds = 5;
}

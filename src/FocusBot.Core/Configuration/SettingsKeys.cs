namespace FocusBot.Core.Configuration;

/// <summary>
/// Centralized settings key constants for ISettingsService storage.
/// </summary>
public static class SettingsKeys
{
    /// <summary>
    /// Boolean flag indicating whether the user has seen the "How it works" guide.
    /// </summary>
    public const string HasSeenHowItWorksGuide = "HasSeenHowItWorksGuide";

    /// <summary>
    /// Boolean flag indicating whether the user has seen the trial welcome dialog (desktop).
    /// </summary>
    public const string TrialWelcomeSeen = "TrialWelcomeSeen";
}

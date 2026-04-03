namespace FocusBot.App.ViewModels;

/// <summary>
/// Service for navigating between main app views (e.g. Focus page and Settings).
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Navigates to the home view (session start page).
    /// </summary>
    void NavigateToHomePage();

    /// <summary>
    /// Navigates to the Settings view.
    /// </summary>
    void NavigateToSettings();

    /// <summary>
    /// Brings the main window to the foreground.
    /// </summary>
    void ActivateMainWindow();
}

namespace FocusBot.App.ViewModels;

/// <summary>
/// Service for navigating between main app views (e.g. Kanban board and Settings).
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Navigates to the Kanban board view.
    /// </summary>
    void NavigateToBoard();

    /// <summary>
    /// Navigates to the Settings view.
    /// </summary>
    void NavigateToSettings();
}

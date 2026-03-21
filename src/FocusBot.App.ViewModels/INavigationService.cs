namespace FocusBot.App.ViewModels;

/// <summary>
/// Service for navigating between main app views (e.g. Focus page and Settings).
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Navigates to the Focus page (main board view).
    /// </summary>
    void NavigateToBoard();

    /// <summary>
    /// Navigates to the Settings view.
    /// </summary>
    void NavigateToSettings();

    /// <summary>
    /// Navigates to the Task Detail view for the specified task.
    /// </summary>
    void NavigateToTaskDetail(string taskId);

    /// <summary>
    /// Navigates to the History view.
    /// </summary>
    void NavigateToHistory();

    /// <summary>
    /// Brings the main window to the foreground.
    /// </summary>
    void ActivateMainWindow();
}

using FocusBot.App.ViewModels;
using FocusBot.App.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace FocusBot.App;

/// <summary>
/// Navigates between the Kanban board and Settings by swapping the main window content.
/// </summary>
public class MainWindowNavigationService(IServiceProvider serviceProvider) : INavigationService
{
    private Window? _window;
    private UIElement? _boardContent;

    /// <summary>
    /// Sets the main window reference. Must be called after the window is created.
    /// </summary>
    public void SetWindow(Window window)
    {
        _window = window;
    }

    /// <inheritdoc />
    public void NavigateToBoard()
    {
        if (_window == null || _boardContent == null)
            return;
        _window.Content = _boardContent;
    }

    /// <inheritdoc />
    public void NavigateToSettings()
    {
        if (_window == null)
            return;
        if (_boardContent == null)
            _boardContent = _window.Content as UIElement;
        var settingsViewModel = serviceProvider.GetRequiredService<SettingsViewModel>();
        _window.Content = new SettingsPage { DataContext = settingsViewModel };
    }
}

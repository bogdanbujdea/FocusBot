using System.Runtime.InteropServices;
using FocusBot.App.ViewModels;
using FocusBot.App.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace FocusBot.App;

/// <summary>
/// Navigates between the Focus page and Settings by swapping the main window content.
/// </summary>
public class MainWindowNavigationService(IServiceProvider serviceProvider) : INavigationService
{
    private Window? _window;
    private UIElement? _boardContent;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>
    /// Sets the main window reference. Must be called after the window is created.
    /// </summary>
    public void SetWindow(Window window)
    {
        _window = window;
    }

    /// <inheritdoc />
    public void NavigateToHomePage()
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

    /// <inheritdoc />
    public void ActivateMainWindow()
    {
        if (_window == null)
            return;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
        SetForegroundWindow(hwnd);
        _window.Activate();
    }
}

using System.Runtime.InteropServices;
using FocusBot.App.ViewModels;
using FocusBot.App.Views;
using FocusBot.Core.Interfaces;
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
    public void NavigateToBoard()
    {
        if (_window == null || _boardContent == null)
            return;
        _window.Content = _boardContent;
        if (_boardContent is KanbanBoardPage page && page.DataContext is KanbanBoardViewModel vm)
            _ = vm.RefreshAiSettingsAsync();
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
    public void NavigateToTaskDetail(string taskId)
    {
        if (_window == null)
            return;
        if (_boardContent == null)
            _boardContent = _window.Content as UIElement;

        using var scope = serviceProvider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        var viewModel = new TaskDetailViewModel(repo, this);
        var page = new TaskDetailPage { DataContext = viewModel };
        _window.Content = page;
        _ = viewModel.InitializeAsync(taskId);
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

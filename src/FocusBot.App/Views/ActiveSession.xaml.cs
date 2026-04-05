using FocusBot.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace FocusBot.App.Views;

public sealed partial class ActiveSession : UserControl
{
    public ActiveSessionViewModel? ViewModel => DataContext as ActiveSessionViewModel;

    public ActiveSession()
    {
        InitializeComponent();
    }

    public string FormatStartTime(DateTime startedAtUtc)
    {
        var local = startedAtUtc.ToLocalTime();
        return local.ToString("h:mm tt");
    }

    public double TimerOpacity(bool isPaused)
    {
        return isPaused ? 0.5 : 1.0;
    }
}

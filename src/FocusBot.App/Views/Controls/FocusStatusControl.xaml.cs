using FocusBot.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace FocusBot.App.Views.Controls;

/// <summary>
/// Displays the current foreground window info and focus classification status during an active session.
/// </summary>
public sealed partial class FocusStatusControl : UserControl
{
    public FocusStatusViewModel ViewModel => (FocusStatusViewModel)DataContext;

    public FocusStatusControl()
    {
        InitializeComponent();
    }
}

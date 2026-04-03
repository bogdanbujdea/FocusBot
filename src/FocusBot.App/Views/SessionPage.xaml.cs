using FocusBot.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace FocusBot.App.Views;

public sealed partial class SessionPage : Page
{
    public SessionPageViewModel ViewModel => (SessionPageViewModel)DataContext;

    public SessionPage()
    {
        InitializeComponent();
    }
}

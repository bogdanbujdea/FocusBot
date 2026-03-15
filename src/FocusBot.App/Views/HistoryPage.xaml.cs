using FocusBot.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace FocusBot.App.Views;

public sealed partial class HistoryPage : Page
{
    public HistoryViewModel ViewModel => (HistoryViewModel)DataContext;

    public HistoryPage()
    {
        InitializeComponent();
    }
}

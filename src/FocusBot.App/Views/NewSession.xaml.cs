using FocusBot.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace FocusBot.App.Views;

public sealed partial class NewSession : UserControl
{
    public NewSessionViewModel? ViewModel => DataContext as NewSessionViewModel;

    public NewSession()
    {
        InitializeComponent();
    }

    private void InfoBar_Closed(InfoBar sender, object args)
    {
        ViewModel?.ClearErrorCommand.Execute(null);
    }
}

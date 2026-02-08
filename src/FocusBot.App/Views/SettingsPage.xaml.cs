using FocusBot.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace FocusBot.App.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel => (SettingsViewModel)DataContext;

    public SettingsPage()
    {
        InitializeComponent();
    }

    private void ApiKeyInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox box && DataContext is SettingsViewModel vm && box.Text != vm.ApiKeySection.ApiKey)
        {
            vm.ApiKeySection.ApiKey = box.Text;
        }
    }
}

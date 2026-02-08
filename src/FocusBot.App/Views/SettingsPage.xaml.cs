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
        if (!TryGetApiKeyUpdateContext(sender, out var box, out var vm))
            return;
        vm!.ApiKeySection.ApiKey = box!.Text;
    }

    private bool TryGetApiKeyUpdateContext(object sender, out TextBox? box, out SettingsViewModel? vm)
    {
        box = sender as TextBox;
        vm = DataContext as SettingsViewModel;
        return box != null && vm != null && box.Text != vm.ApiKeySection.ApiKey;
    }
}

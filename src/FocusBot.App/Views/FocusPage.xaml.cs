using FocusBot.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FocusBot.App.Views;

public sealed partial class FocusPage : Page
{
    public FocusPageViewModel ViewModel => (FocusPageViewModel)DataContext;

    public FocusPage()
    {
        InitializeComponent();
        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not FocusPageViewModel vm)
            return;
        vm.RefreshExtensionConnectionState();
        _ = vm.RefreshAiSettingsAsync();
        vm.ShowHowItWorksRequested += OnShowHowItWorksRequested;
        _ = InitializeFirstRunAsync(vm);
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is FocusPageViewModel vm)
        {
            vm.ShowHowItWorksRequested -= OnShowHowItWorksRequested;
        }
    }

    private async Task InitializeFirstRunAsync(FocusPageViewModel vm)
    {
        var hasSeen = await vm.GetHasSeenHowItWorksGuideAsync();
        if (!hasSeen)
        {
            var result = await ShowHowItWorksDialogAsync();
            await vm.SetHasSeenHowItWorksGuideAsync();

            if (result == ContentDialogResult.Secondary || !vm.IsAiConfigured)
                vm.OpenSettingsCommand.Execute(null);
        }
    }

    private void OnShowHowItWorksRequested(object? sender, EventArgs e) => _ = ShowHowItWorksDialogAsync();

    private async Task<ContentDialogResult> ShowHowItWorksDialogAsync()
    {
        if (XamlRoot == null)
            return ContentDialogResult.None;
        var dialog = new HowItWorksDialog { XamlRoot = XamlRoot };
        return await dialog.ShowAsync();
    }
}


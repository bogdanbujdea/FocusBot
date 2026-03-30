using System.ComponentModel;
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
        vm.ShowHowItWorksRequested += OnShowHowItWorksRequested;
        vm.ShowBYOKKeyPromptRequested += OnShowBYOKKeyPromptRequested;
        vm.AccountSection.PropertyChanged += OnAccountSectionPropertyChanged;
        _ = InitializeFirstRunAsync(vm);
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not FocusPageViewModel vm)
            return;
        vm.ShowHowItWorksRequested -= OnShowHowItWorksRequested;
        vm.ShowBYOKKeyPromptRequested -= OnShowBYOKKeyPromptRequested;
        vm.AccountSection.PropertyChanged -= OnAccountSectionPropertyChanged;
    }

    private async void OnAccountSectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AccountSettingsViewModel.IsAuthenticated))
            return;
        if (DataContext is not FocusPageViewModel vm)
            return;
        if (!vm.AccountSection.IsAuthenticated)
            return;
        await TryShowTrialWelcomeAsync(vm);
    }

    private async Task InitializeFirstRunAsync(FocusPageViewModel vm)
    {
        var hasSeen = await vm.GetHasSeenHowItWorksGuideAsync();
        if (!hasSeen)
        {
            var result = await ShowHowItWorksDialogAsync();
            await vm.SetHasSeenHowItWorksGuideAsync();

            if (result == ContentDialogResult.Secondary || !vm.AccountSection.IsAuthenticated)
                vm.OpenSettingsCommand.Execute(null);
        }

        await TryShowTrialWelcomeAsync(vm);
    }

    private async Task TryShowTrialWelcomeAsync(FocusPageViewModel vm)
    {
        if (!await vm.ShouldShowTrialWelcomeAsync())
            return;
        if (XamlRoot == null)
            return;

        var dialog = new TrialWelcomeDialog { XamlRoot = XamlRoot };
        var result = await dialog.ShowAsync();
        await vm.SetHasSeenTrialWelcomeAsync();
        if (result == ContentDialogResult.Secondary)
            dialog.OpenBillingInBrowser();
    }

    private void OnShowHowItWorksRequested(object? sender, EventArgs e) =>
        _ = ShowHowItWorksDialogAsync();

    private async void OnShowBYOKKeyPromptRequested(object? sender, EventArgs e)
    {
        if (XamlRoot == null)
            return;
        if (DataContext is not FocusPageViewModel vm)
            return;

        var dialog = new BYOKKeyPromptDialog { XamlRoot = XamlRoot };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
            vm.OpenSettingsCommand.Execute(null);
    }

    private async Task<ContentDialogResult> ShowHowItWorksDialogAsync()
    {
        if (XamlRoot == null)
            return ContentDialogResult.None;
        var dialog = new HowItWorksDialog { XamlRoot = XamlRoot };
        return await dialog.ShowAsync();
    }
}

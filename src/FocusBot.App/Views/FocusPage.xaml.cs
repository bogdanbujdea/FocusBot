using FocusBot.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FocusBot.App.Views;

public sealed partial class FocusPage : Page
{
    public FocusPageViewModel ViewModel => (FocusPageViewModel)DataContext;

    private DispatcherTimer? _trialTimer;

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
        vm.TrialExpired += OnTrialExpired;
        _ = InitializeTrialAndFirstRunAsync(vm);
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        StopTrialTimer();
        if (DataContext is FocusPageViewModel vm)
        {
            vm.TrialExpired -= OnTrialExpired;
        }
    }

    private async Task InitializeTrialAndFirstRunAsync(FocusPageViewModel vm)
    {
        var hasSeen = await vm.GetHasSeenHowItWorksGuideAsync();
        if (!hasSeen)
        {
            var result = await ShowHowItWorksDialogAsync();
            await vm.SetHasSeenHowItWorksGuideAsync();

            // Start trial automatically on first run if no API key is configured
            if (!vm.IsAiConfigured)
            {
                await vm.StartTrialAsync();
            }

            if (result == ContentDialogResult.Secondary)
                vm.OpenSettingsCommand.Execute(null);
            else if (!vm.IsAiConfigured && !vm.IsTrialActive)
                vm.OpenSettingsCommand.Execute(null);
        }

        // Always refresh trial state and start timer if trial is active
        await vm.RefreshTrialStateAsync();
        if (vm.IsTrialActive)
        {
            StartTrialTimer(vm);
        }
    }

    private void StartTrialTimer(FocusPageViewModel vm)
    {
        StopTrialTimer();
        _trialTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _trialTimer.Tick += async (_, _) =>
        {
            await vm.UpdateTrialTimeRemainingAsync();
            if (!vm.IsTrialActive)
            {
                StopTrialTimer();
            }
        };
        _trialTimer.Start();
    }

    private void StopTrialTimer()
    {
        _trialTimer?.Stop();
        _trialTimer = null;
    }

    private async void OnTrialExpired(object? sender, EventArgs e)
    {
        StopTrialTimer();
        if (XamlRoot == null)
            return;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Free Trial Expired",
            Content = "Your 24-hour free trial has ended. To continue using AI-powered focus tracking, please subscribe to Foqus Premium or enter your own API key.",
            PrimaryButtonText = "Open Settings",
            CloseButtonText = "Later",
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            ViewModel.OpenSettingsCommand.Execute(null);
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

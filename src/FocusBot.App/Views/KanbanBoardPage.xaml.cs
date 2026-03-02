using FocusBot.App.ViewModels;
using FocusBot.Core.Entities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace FocusBot.App.Views;

public sealed partial class KanbanBoardPage : Page
{
    public KanbanBoardViewModel ViewModel => (KanbanBoardViewModel)DataContext;

    private DispatcherTimer? _trialTimer;

    public KanbanBoardPage()
    {
        InitializeComponent();
        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not KanbanBoardViewModel vm)
            return;
        SyncPopupToViewModel(vm.ShowAddTaskInput);
        SyncEditPopupToViewModel(vm.ShowEditTaskInput);
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(KanbanBoardViewModel.ShowAddTaskInput))
                SyncPopupToViewModel(vm.ShowAddTaskInput);
            else if (args.PropertyName == nameof(KanbanBoardViewModel.ShowEditTaskInput))
                SyncEditPopupToViewModel(vm.ShowEditTaskInput);
        };
        vm.ShowHowItWorksRequested += OnShowHowItWorksRequested;
        vm.TrialExpired += OnTrialExpired;
        AddTaskPopup.Closed += OnAddTaskPopupClosed;
        AddTaskPopup.Opened += OnAddTaskPopupOpened;
        EditTaskPopup.Closed += OnEditTaskPopupClosed;
        EditTaskPopup.Opened += OnEditTaskPopupOpened;
        _ = InitializeTrialAndFirstRunAsync(vm);
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        StopTrialTimer();
        if (DataContext is KanbanBoardViewModel vm)
        {
            vm.TrialExpired -= OnTrialExpired;
        }
    }

    private async Task InitializeTrialAndFirstRunAsync(KanbanBoardViewModel vm)
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

    private void StartTrialTimer(KanbanBoardViewModel vm)
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
            Content = "Your 24-hour free trial has ended. To continue using AI-powered focus tracking, please subscribe to FocusBot Pro or enter your own API key.",
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

    private void OnAddTaskPopupOpened(object? sender, object e)
    {
        AddTaskOverlay.Width = RootGrid.ActualWidth;
        AddTaskOverlay.Height = RootGrid.ActualHeight;
        AddTaskDescriptionBox.Focus(FocusState.Programmatic);
    }

    private void AddTaskButton_Click(object sender, RoutedEventArgs e)
    {
        SyncPopupToViewModel(ViewModel.ShowAddTaskInput);
    }

    private void SyncPopupToViewModel(bool show)
    {
        if (show)
        {
            AddTaskPopup.PlacementTarget = RootGrid;
            AddTaskPopup.XamlRoot = RootGrid.XamlRoot;
            AddTaskPopup.IsOpen = true;
        }
        else
        {
            AddTaskPopup.IsOpen = false;
        }
    }

    private void AddTaskPopup_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter)
            return;
        e.Handled = true;
        if (ViewModel.AddTaskCommand.CanExecute(null))
            ViewModel.AddTaskCommand.Execute(null);
    }

    private void OnAddTaskPopupClosed(object? sender, object e)
    {
        if (DataContext is KanbanBoardViewModel vm)
            vm.ShowAddTaskInput = false;
    }

    private void OnEditTaskPopupOpened(object? sender, object e)
    {
        EditTaskOverlay.Width = RootGrid.ActualWidth;
        EditTaskOverlay.Height = RootGrid.ActualHeight;
        EditTaskDescriptionBox.Focus(FocusState.Programmatic);
    }

    private void EditTaskPopup_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter)
            return;
        e.Handled = true;
        if (ViewModel.SaveEditTaskCommand.CanExecute(null))
            ViewModel.SaveEditTaskCommand.Execute(null);
    }

    private FrameworkElement? _editPopupPlacementTarget;

    private void SyncEditPopupToViewModel(bool show)
    {
        if (show && _editPopupPlacementTarget != null)
        {
            EditTaskPopup.PlacementTarget = RootGrid;
            EditTaskPopup.XamlRoot = RootGrid.XamlRoot;
            EditTaskPopup.IsOpen = true;
        }
        else
        {
            EditTaskPopup.IsOpen = false;
        }
    }

    private void OnEditTaskPopupClosed(object? sender, object e)
    {
        if (DataContext is KanbanBoardViewModel vm)
            vm.ShowEditTaskInput = false;
    }

    private void EditTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
            return;
        if (element.DataContext is not UserTask task)
            return;
        _editPopupPlacementTarget = element;
        _ = ViewModel.BeginEditTaskCommand.ExecuteAsync(task.TaskId);
    }

    private void TaskCard_DragStarting(UIElement sender, DragStartingEventArgs e)
    {
        if (!TryGetTaskId(sender, out var taskId))
            return;
        e.Data.SetText(taskId!);
    }

    private static bool TryGetTaskId(object sender, out string? taskId)
    {
        taskId = (sender as FrameworkElement)?.Tag as string;
        return taskId != null;
    }

    private void Column_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
    }

    private async void Column_Drop(object sender, DragEventArgs e)
    {
        if (!TryGetDropTargetStatus(sender, out var status))
            return;
        var text = await e.DataView.GetTextAsync();
        if (string.IsNullOrEmpty(text))
            return;
        await ViewModel.MoveToStatusAsync(text, status!);
    }

    private static bool TryGetDropTargetStatus(object sender, out string? status)
    {
        status = (sender as FrameworkElement)?.Tag as string;
        return status != null;
    }
}

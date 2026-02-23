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

    public KanbanBoardPage()
    {
        InitializeComponent();
        Loaded += OnPageLoaded;
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
        AddTaskPopup.Closed += OnAddTaskPopupClosed;
        AddTaskPopup.Opened += OnAddTaskPopupOpened;
        EditTaskPopup.Closed += OnEditTaskPopupClosed;
        EditTaskPopup.Opened += OnEditTaskPopupOpened;
        _ = TryShowFirstRunGuideAsync(vm);
    }

    private async Task TryShowFirstRunGuideAsync(KanbanBoardViewModel vm)
    {
        var hasSeen = await vm.GetHasSeenHowItWorksGuideAsync();
        if (hasSeen)
            return;
        await ShowHowItWorksDialogAsync();
        await vm.SetHasSeenHowItWorksGuideAsync();
    }

    private void OnShowHowItWorksRequested(object? sender, EventArgs e) => _ = ShowHowItWorksDialogAsync();

    private async Task ShowHowItWorksDialogAsync()
    {
        if (XamlRoot == null)
            return;
        var dialog = new HowItWorksDialog { XamlRoot = XamlRoot };
        await dialog.ShowAsync();
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

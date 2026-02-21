using FocusBot.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

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
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(KanbanBoardViewModel.ShowAddTaskInput))
                SyncPopupToViewModel(vm.ShowAddTaskInput);
        };
        AddTaskPopup.Closed += OnAddTaskPopupClosed;
    }

    private void AddTaskButton_Click(object sender, RoutedEventArgs e)
    {
        SyncPopupToViewModel(ViewModel.ShowAddTaskInput);
    }

    private void SyncPopupToViewModel(bool show)
    {
        if (show)
        {
            AddTaskPopup.PlacementTarget = AddTaskButton;
            AddTaskPopup.XamlRoot = AddTaskButton.XamlRoot;
            AddTaskPopup.IsOpen = true;
        }
        else
        {
            AddTaskPopup.IsOpen = false;
        }
    }

    private void OnAddTaskPopupClosed(object? sender, object e)
    {
        if (DataContext is KanbanBoardViewModel vm)
            vm.ShowAddTaskInput = false;
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

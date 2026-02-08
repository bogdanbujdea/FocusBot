using FocusBot.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
        if (DataContext is not KanbanBoardViewModel vm) return;
        UpdateAddTaskPanelVisibility(vm.ShowAddTaskInput);
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(KanbanBoardViewModel.ShowAddTaskInput))
                UpdateAddTaskPanelVisibility(vm.ShowAddTaskInput);
        };
    }

    private void AddTaskButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateAddTaskPanelVisibility(ViewModel.ShowAddTaskInput);
    }

    private void UpdateAddTaskPanelVisibility(bool show)
    {
        AddTaskPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private void TaskCard_DragStarting(Microsoft.UI.Xaml.UIElement sender, Microsoft.UI.Xaml.DragStartingEventArgs e)
    {
        if (!TryGetTaskId(sender, out var taskId))
            return;
        e.Data.SetText(taskId!);
    }

    private static bool TryGetTaskId(object sender, out string? taskId)
    {
        taskId = (sender as Microsoft.UI.Xaml.FrameworkElement)?.Tag as string;
        return taskId != null;
    }

    private void Column_DragOver(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
    }

    private async void Column_Drop(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        if (!TryGetDropTargetStatus(sender, out var status))
            return;
        var text = await e.DataView.GetTextAsync();
        if (string.IsNullOrEmpty(text)) return;
        await ViewModel.MoveToStatusAsync(text, status!);
    }

    private static bool TryGetDropTargetStatus(object sender, out string? status)
    {
        status = (sender as Microsoft.UI.Xaml.FrameworkElement)?.Tag as string;
        return status != null;
    }
}

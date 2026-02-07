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
        if (sender is Microsoft.UI.Xaml.FrameworkElement fe && fe.Tag is string taskId)
            e.Data.SetText(taskId);
    }

    private void Column_DragOver(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
    }

    private async void Column_Drop(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        if (sender is not Microsoft.UI.Xaml.FrameworkElement column || column.Tag is not string status)
            return;
        var text = await e.DataView.GetTextAsync();
        if (string.IsNullOrEmpty(text)) return;
        await ViewModel.MoveToStatusAsync(text, status);
    }
}

using FocusBot.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace FocusBot.App.Views;

public sealed partial class TaskDetailPage : Page
{
    public TaskDetailViewModel ViewModel => (TaskDetailViewModel)DataContext;

    public TaskDetailPage()
    {
        InitializeComponent();
    }
}

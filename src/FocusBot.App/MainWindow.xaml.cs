using FocusBot.App.ViewModels;
using FocusBot.App.Views;
using Microsoft.UI.Xaml;

namespace FocusBot.App
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow(KanbanBoardViewModel viewModel)
        {
            InitializeComponent();
            Content = new KanbanBoardPage { DataContext = viewModel };
        }
    }
}

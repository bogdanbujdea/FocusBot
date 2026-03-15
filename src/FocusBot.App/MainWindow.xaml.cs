using FocusBot.App.ViewModels;
using FocusBot.App.Views;
using Microsoft.UI.Xaml;

namespace FocusBot.App
{
    public sealed partial class MainWindow : Window
    {
        private readonly FocusPage _kanbanPage;

        public MainWindow(
            FocusPageViewModel viewModel,
            INavigationService navigationService
        )
        {
            InitializeComponent();

            _kanbanPage = new FocusPage { DataContext = viewModel };
            Content = _kanbanPage;

            // Set default window size
            AppWindow.Resize(new Windows.Graphics.SizeInt32(1100, 1500));
        }
    }
}

using FocusBot.App.ViewModels;
using FocusBot.App.Views;
using Microsoft.UI.Xaml;

namespace FocusBot.App
{
    public sealed partial class MainWindow : Window
    {
        private readonly FocusPage _focusPage;

        public MainWindow(FocusPageViewModel viewModel)
        {
            InitializeComponent();

            _focusPage = new FocusPage { DataContext = viewModel };
            Content = _focusPage;

            // Set default window size
            AppWindow.Resize(new Windows.Graphics.SizeInt32(1500, 1500));
        }
    }
}

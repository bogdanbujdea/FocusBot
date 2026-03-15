using FocusBot.App.ViewModels;
using FocusBot.App.Views;
using FocusBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace FocusBot.App
{
    public sealed partial class MainWindow : Window
    {
        private readonly FocusPageViewModel _kanbanViewModel;
        private readonly IIntegrationService _integrationService;
        private readonly IServiceProvider _services;
        private readonly FocusPage _kanbanPage;

        public MainWindow(
            FocusPageViewModel viewModel,
            INavigationService navigationService,
            IIntegrationService integrationService,
            IServiceProvider services)
        {
            InitializeComponent();
            _kanbanViewModel = viewModel;
            _integrationService = integrationService;
            _services = services;

            _kanbanPage = new FocusPage { DataContext = viewModel };
            Content = _kanbanPage;

            // Set default window size
            AppWindow.Resize(new Windows.Graphics.SizeInt32(1100, 1000));
        }
    }
}

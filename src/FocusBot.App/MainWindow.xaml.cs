using FocusBot.App.ViewModels;
using FocusBot.App.Views;
using FocusBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace FocusBot.App
{
    public sealed partial class MainWindow : Window
    {
        private readonly KanbanBoardViewModel _kanbanViewModel;
        private readonly IIntegrationService _integrationService;
        private readonly IServiceProvider _services;
        private readonly KanbanBoardPage _kanbanPage;

        public MainWindow(
            KanbanBoardViewModel viewModel,
            INavigationService navigationService,
            IIntegrationService integrationService,
            IServiceProvider services)
        {
            InitializeComponent();
            _kanbanViewModel = viewModel;
            _integrationService = integrationService;
            _services = services;

            _kanbanPage = new KanbanBoardPage { DataContext = viewModel };
            Content = _kanbanPage;
        }
    }
}

using FocusBot.App.ViewModels;
using FocusBot.App.Views;
using FocusBot.Core.DTOs;
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
        private CompanionView? _companionView;
        private CompanionViewModel? _companionViewModel;

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

            _kanbanViewModel.CompanionModeRequested += OnCompanionModeRequested;
        }

        private void OnCompanionModeRequested(object? sender, TaskStartedPayload payload)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _companionViewModel = _services.GetRequiredService<CompanionViewModel>();
                _companionViewModel.ApplyTaskStarted(payload);
                _companionViewModel.ReturnToStandalone += OnReturnToStandalone;

                _companionView = new CompanionView(_companionViewModel);
                Content = _companionView;
            });
        }

        private void OnReturnToStandalone(object? sender, EventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                Content = _kanbanPage;

                if (_companionViewModel != null)
                    _companionViewModel.ReturnToStandalone -= OnReturnToStandalone;
                _companionViewModel = null;
                _companionView = null;
            });
        }
    }
}

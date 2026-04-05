using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;

namespace FocusBot.App.ViewModels;

public partial class SessionPageViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly ISessionCoordinator _coordinator;
    private readonly IUIThreadDispatcher _dispatcher;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveSession))]
    private ActiveSessionViewModel? _activeSession;

    public NewSessionViewModel NewSession { get; }

    public bool HasActiveSession => ActiveSession != null;

    public SessionPageViewModel(
        NewSessionViewModel newSession,
        INavigationService navigationService,
        ISessionCoordinator coordinator,
        IUIThreadDispatcher dispatcher)
    {
        NewSession = newSession;
        _navigationService = navigationService;
        _coordinator = coordinator;
        _dispatcher = dispatcher;

        _coordinator.StateChanged += OnCoordinatorStateChanged;
    }

    /// <summary>
    /// Loads any existing active session from the API. Call after the view is ready.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _coordinator.InitializeAsync();
    }

    private void OnCoordinatorStateChanged(SessionState state, SessionChangeType changeType)
    {
        _ = _dispatcher.RunOnUIThreadAsync(() =>
        {
            if (state.HasActiveSession && ActiveSession == null)
            {
                var vm = new ActiveSessionViewModel(_dispatcher, _coordinator);
                _ = vm.LoadAsync(state.ActiveSession!);
                ActiveSession = vm;
            }
            else if (!state.HasActiveSession && ActiveSession != null)
            {
                var previous = ActiveSession;
                ActiveSession = null;
                previous?.Dispose();
            }

            return Task.CompletedTask;
        });
    }

    [RelayCommand]
    private void OpenSettings()
    {
        _navigationService.NavigateToSettings();
    }
}

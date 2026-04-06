using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;

namespace FocusBot.App.ViewModels;

public partial class SessionPageViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly ISessionCoordinator _coordinator;
    private readonly IForegroundClassificationCoordinator _classificationCoordinator;
    private readonly IExtensionPresenceService _presenceService;
    private readonly IUIThreadDispatcher _dispatcher;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveSession))]
    private ActiveSessionViewModel? _activeSession;

    [ObservableProperty]
    private bool _isExtensionConnected;

    public NewSessionViewModel NewSession { get; }

    public bool HasActiveSession => ActiveSession != null;

    public SessionPageViewModel(
        NewSessionViewModel newSession,
        INavigationService navigationService,
        ISessionCoordinator coordinator,
        IForegroundClassificationCoordinator classificationCoordinator,
        IExtensionPresenceService presenceService,
        IUIThreadDispatcher dispatcher)
    {
        NewSession = newSession;
        _navigationService = navigationService;
        _coordinator = coordinator;
        _classificationCoordinator = classificationCoordinator;
        _presenceService = presenceService;
        _dispatcher = dispatcher;

        _coordinator.StateChanged += OnCoordinatorStateChanged;

        IsExtensionConnected = _presenceService.IsExtensionOnline;
        _presenceService.ExtensionConnected += OnExtensionConnected;
        _presenceService.ExtensionDisconnected += OnExtensionDisconnected;
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
                var vm = new ActiveSessionViewModel(_dispatcher, _coordinator, _classificationCoordinator);
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

    private void OnExtensionConnected()
    {
        _ = _dispatcher.RunOnUIThreadAsync(() =>
        {
            IsExtensionConnected = true;
            return Task.CompletedTask;
        });
    }

    private void OnExtensionDisconnected()
    {
        _ = _dispatcher.RunOnUIThreadAsync(() =>
        {
            IsExtensionConnected = false;
            return Task.CompletedTask;
        });
    }

    [RelayCommand]
    private void OpenSettings()
    {
        _navigationService.NavigateToSettings();
    }
}

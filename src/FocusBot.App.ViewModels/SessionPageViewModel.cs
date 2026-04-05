using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;

namespace FocusBot.App.ViewModels;

public partial class SessionPageViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly IFocusBotApiClient _apiClient;
    private readonly IUIThreadDispatcher _dispatcher;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveSession))]
    private ActiveSessionViewModel? _activeSession;

    public NewSessionViewModel NewSession { get; }

    public bool HasActiveSession => ActiveSession != null;

    public SessionPageViewModel(
        NewSessionViewModel newSession,
        INavigationService navigationService,
        IFocusBotApiClient apiClient,
        IUIThreadDispatcher dispatcher)
    {
        NewSession = newSession;
        _navigationService = navigationService;
        _apiClient = apiClient;
        _dispatcher = dispatcher;

        NewSession.OnSessionStarted = OnSessionStarted;
    }

    /// <summary>
    /// Loads any existing active session from the API. Call after the view is ready.
    /// </summary>
    public async Task InitializeAsync()
    {
        var existing = await _apiClient.GetActiveSessionAsync();
        if (existing != null)
        {
            SetActiveSession(existing);
        }
    }

    private void OnSessionStarted(ApiSessionResponse session)
    {
        SetActiveSession(session);
    }

    private void SetActiveSession(ApiSessionResponse session)
    {
        ActiveSession?.Dispose();
        var vm = new ActiveSessionViewModel(_dispatcher);
        vm.SetSession(session);
        ActiveSession = vm;
    }

    [RelayCommand]
    private void OpenSettings()
    {
        _navigationService.NavigateToSettings();
    }
}

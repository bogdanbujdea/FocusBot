using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;

namespace FocusBot.App.ViewModels;

public partial class NewSessionViewModel : ObservableObject
{
    private readonly ISessionCoordinator _coordinator;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private string _sessionTitle = string.Empty;

    [ObservableProperty]
    private string _sessionContext = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private SessionStartState _state = SessionStartState.Idle;

    public NewSessionViewModel(ISessionCoordinator coordinator)
    {
        _coordinator = coordinator;
        _coordinator.StateChanged += OnCoordinatorStateChanged;
    }

    private void OnCoordinatorStateChanged(SessionState state, SessionChangeType changeType)
    {
        if (
            changeType == SessionChangeType.Started
            && state is { HasError: false, HasActiveSession: true }
        )
        {
            SessionTitle = string.Empty;
            SessionContext = string.Empty;
            State = SessionStartState.Idle;
        }
        else if (state is { HasError: true, HasActiveSession: false })
        {
            State = SessionStartState.Error(state.ErrorMessage ?? "Unknown error");
        }
        else if (state is { HasError: false, HasActiveSession: false })
        {
            State = SessionStartState.Idle;
        }
    }

    private bool CanStartSession =>
        State != SessionStartState.Loading && !string.IsNullOrWhiteSpace(SessionTitle);

    private bool CanStart() => CanStartSession;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        await _coordinator.StartAsync(SessionTitle.Trim(), SessionContext?.Trim());
    }

    [RelayCommand]
    private void ClearError()
    {
        _coordinator.ClearError();
    }
}

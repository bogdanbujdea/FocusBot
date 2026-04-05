using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;

namespace FocusBot.App.ViewModels;

public partial class NewSessionViewModel : ObservableObject
{
    private readonly IFocusBotApiClient _apiClient;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private string _sessionTitle = string.Empty;

    [ObservableProperty]
    private string _sessionContext = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private SessionStartState _state = SessionStartState.Idle;

    public event Action<ApiSessionResponse>? OnSessionStarted;

    public NewSessionViewModel(IFocusBotApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    private bool CanStartSession =>
        State != SessionStartState.Loading && !string.IsNullOrWhiteSpace(SessionTitle);

    private bool CanStart() => CanStartSession;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        State = SessionStartState.Loading;
        try
        {
            var result = await _apiClient.StartSessionAsync(
                new StartSessionPayload(SessionTitle.Trim(), SessionContext?.Trim())
            );

            if (result.IsSuccess)
            {
                OnSessionStarted?.Invoke(result.Value!);
                SessionTitle = string.Empty;
                SessionContext = string.Empty;
                State = SessionStartState.Idle;
            }
            else
            {
                State = SessionStartState.Error(result.ErrorMessage ?? "Unknown error");
            }
        }
        catch (Exception ex)
        {
            State = SessionStartState.Error(ex.Message);
        }
    }

    [RelayCommand]
    private void ClearError()
    {
        State = SessionStartState.Idle;
    }
}

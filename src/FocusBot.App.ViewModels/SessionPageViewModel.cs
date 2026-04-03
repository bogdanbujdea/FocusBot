using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FocusBot.App.ViewModels;

public partial class SessionPageViewModel(
    NewSessionViewModel newSession,
    INavigationService navigationService
) : ObservableObject
{
    public NewSessionViewModel NewSession { get; } = newSession;

    [RelayCommand]
    private void OpenSettings()
    {
        navigationService.NavigateToSettings();
    }
}

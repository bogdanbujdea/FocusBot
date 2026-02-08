using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FocusBot.App.ViewModels;

/// <summary>
/// ViewModel for the settings view.
/// </summary>
public partial class SettingsViewModel(
    ApiKeySettingsViewModel apiKeySection,
    INavigationService navigationService
) : ObservableObject
{
    public ApiKeySettingsViewModel ApiKeySection { get; } = apiKeySection;

    [RelayCommand]
    private void Back()
    {
        navigationService.NavigateToBoard();
    }
}

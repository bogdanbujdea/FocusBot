using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FocusBot.App.ViewModels;

/// <summary>
/// ViewModel for the settings view.
/// </summary>
public partial class SettingsViewModel(
    ApiKeySettingsViewModel apiKeySection,
    OverlaySettingsViewModel overlaySection,
    AccountSettingsViewModel accountSection,
    PlanSelectionViewModel planSection,
    INavigationService navigationService
) : ObservableObject
{
    public ApiKeySettingsViewModel ApiKeySection { get; } = apiKeySection;

    public OverlaySettingsViewModel OverlaySection { get; } = overlaySection;

    public AccountSettingsViewModel AccountSection { get; } = accountSection;

    public PlanSelectionViewModel PlanSection { get; } = planSection;

    [RelayCommand]
    private void Back()
    {
        navigationService.NavigateToHomePage();
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FocusBot.App.ViewModels;

public partial class SessionPageViewModel(INavigationService navigationService) : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _context = string.Empty;

    [RelayCommand]
    private void Start()
    {
        // Placeholder: implement session start logic later
    }

    [RelayCommand]
    private void OpenSettings()
    {
        navigationService.NavigateToSettings();
    }
}

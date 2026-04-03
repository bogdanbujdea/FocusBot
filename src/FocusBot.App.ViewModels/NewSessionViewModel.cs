using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FocusBot.App.ViewModels;

public partial class NewSessionViewModel : ObservableObject
{
    [ObservableProperty]
    private string _sessionTitle = string.Empty;

    [ObservableProperty]
    private string _sessionContext = string.Empty;

    [RelayCommand]
    private void Start()
    {
        // Placeholder: implement session start logic later
    }
}

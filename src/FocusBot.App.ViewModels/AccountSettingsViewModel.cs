using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FocusBot.App.ViewModels;

public partial class AccountSettingsViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly ISessionCoordinator _coordinator;
    private readonly ILogger<AccountSettingsViewModel> _logger;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private bool _isAuthenticated;

    [ObservableProperty]
    private string _userEmail = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isStatusError;

    public AccountSettingsViewModel(IAuthService authService, ISessionCoordinator coordinator, ILogger<AccountSettingsViewModel> logger)
    {
        _authService = authService;
        _coordinator = coordinator;
        _logger = logger;

        IsAuthenticated = _authService.IsAuthenticated;
        _authService.AuthStateChanged += OnAuthStateChanged;
        _ = LoadUserEmailAsync();
    }

    private async void OnAuthStateChanged()
    {
        var authenticated = _authService.IsAuthenticated;
        if (authenticated && LooksLikeMagicLinkFlowStatus(StatusMessage))
        {
            StatusMessage = "Welcome to Foqus!";
            IsStatusError = false;
        }

        IsAuthenticated = authenticated;
        await LoadUserEmailAsync();
    }

    private static bool LooksLikeMagicLinkFlowStatus(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        return message.Contains("magic link", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Sending magic link", StringComparison.OrdinalIgnoreCase);
    }

    private async Task LoadUserEmailAsync()
    {
        if (_authService.IsAuthenticated)
        {
            UserEmail = await _authService.GetUserEmailAsync() ?? string.Empty;
        }
        else
        {
            UserEmail = string.Empty;
        }
    }

    [RelayCommand]
    private async Task SendMagicLinkAsync()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            StatusMessage = "Please enter a valid email address.";
            IsStatusError = true;
            return;
        }

        IsBusy = true;
        IsStatusError = false;
        StatusMessage = "Sending magic link...";

        try
        {
            var success = await _authService.SignInWithMagicLinkAsync(Email.Trim());
            if (success)
            {
                StatusMessage = "Magic link sent. Open it on this device to finish signing in.";
                IsStatusError = false;
            }
            else
            {
                StatusMessage = "Could not send magic link. Check configuration or try again.";
                IsStatusError = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send magic link");
            StatusMessage = "Unexpected error while sending magic link.";
            IsStatusError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        IsBusy = true;
        IsStatusError = false;
        StatusMessage = "Signing out...";

        try
        {
            await _authService.SignOutAsync();
            _coordinator.Reset();
            StatusMessage = "Signed out.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sign out");
            StatusMessage = "Error while signing out.";
            IsStatusError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }
}


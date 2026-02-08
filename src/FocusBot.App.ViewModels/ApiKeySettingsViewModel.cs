using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FocusBot.App.ViewModels;

/// <summary>
/// ViewModel for the API key settings section (load, save, clear, edit).
/// </summary>
public partial class ApiKeySettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<ApiKeySettingsViewModel> _logger;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _maskedApiKeyDisplay = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMaskedDisplay))]
    [NotifyPropertyChangedFor(nameof(ShowInputArea))]
    private bool _isApiKeyConfigured;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMaskedDisplay))]
    [NotifyPropertyChangedFor(nameof(ShowInputArea))]
    private bool _isEditing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private bool _isSaving;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public bool ShowMaskedDisplay => ShouldShowMaskedDisplay();

    public bool ShowInputArea => ShouldShowInputArea();

    public bool CanSave => CanSaveApiKey();

    public ApiKeySettingsViewModel(
        ISettingsService settingsService,
        ILogger<ApiKeySettingsViewModel> logger)
    {
        _settingsService = settingsService;
        _logger = logger;

        _ = LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            var existingKey = await _settingsService.GetApiKeyAsync();
            IsApiKeyConfigured = !string.IsNullOrWhiteSpace(existingKey);
            IsEditing = false;
            ApiKey = string.Empty;

            if (CanShowMaskedKey(existingKey))
            {
                MaskedApiKeyDisplay = "********" + existingKey![^4..];
            }
            else
            {
                MaskedApiKeyDisplay = string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading settings");
            StatusMessage = "Error loading settings";
        }
    }

    [RelayCommand]
    private async Task SaveApiKeyAsync()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            StatusMessage = "Please enter a valid API key";
            return;
        }

        IsSaving = true;
        StatusMessage = "Saving...";

        try
        {
            await _settingsService.SetApiKeyAsync(ApiKey);
            MaskedApiKeyDisplay = "********" + ApiKey[^4..];
            IsApiKeyConfigured = true;
            IsEditing = false;
            ApiKey = string.Empty;
            StatusMessage = "API key saved.";

            _logger.LogInformation("API key saved");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving API key");
            StatusMessage = "Error saving API key";
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void OpenEdit()
    {
        IsEditing = true;
        ApiKey = string.Empty;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        ApiKey = string.Empty;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private async Task ClearApiKeyAsync()
    {
        IsSaving = true;

        try
        {
            await _settingsService.ClearApiKeyAsync();
            IsApiKeyConfigured = false;
            IsEditing = false;
            MaskedApiKeyDisplay = string.Empty;
            ApiKey = string.Empty;
            StatusMessage = "API key cleared";

            _logger.LogInformation("API key cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing API key");
            StatusMessage = "Error clearing API key";
        }
        finally
        {
            IsSaving = false;
        }
    }

    private bool ShouldShowMaskedDisplay() => IsApiKeyConfigured && !IsEditing;

    private bool ShouldShowInputArea() => !IsApiKeyConfigured || IsEditing;

    private bool CanSaveApiKey() => !IsSaving && HasValidApiKey();

    private bool HasValidApiKey() => !string.IsNullOrWhiteSpace(ApiKey);

    private bool CanShowMaskedKey(string? key) => IsApiKeyConfigured && key != null && key.Length >= 4;
}

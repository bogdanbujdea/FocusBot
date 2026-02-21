using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusBot.Core.Interfaces;
using FocusBot.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace FocusBot.App.ViewModels;

/// <summary>
/// ViewModel for the API key settings section (load, save, clear, edit) and provider/model selection.
/// </summary>
public partial class ApiKeySettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<ApiKeySettingsViewModel> _logger;
    private bool _isLoading;

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

    [ObservableProperty]
    private string _apiKeyLabel = "Enter your API key";

    [ObservableProperty]
    private string _apiKeyHelpUrl = string.Empty;

    [ObservableProperty]
    private Uri? _apiKeyHelpUri;

    public ObservableCollection<ProviderInfo> Providers { get; } = new(LlmProviderConfig.Providers);
    public ObservableCollection<ModelInfo> AvailableModels { get; } = new();

    [ObservableProperty]
    private ProviderInfo? _selectedProvider;

    [ObservableProperty]
    private ModelInfo? _selectedModel;

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
        _isLoading = true;
        try
        {
            var savedProviderId = await _settingsService.GetProviderAsync();
            SelectedProvider = Providers.FirstOrDefault(p => p.ProviderId == savedProviderId)
                ?? Providers.First();

            var savedModelId = await _settingsService.GetModelAsync();
            SelectedModel = AvailableModels.FirstOrDefault(m => m.ModelId == savedModelId)
                ?? AvailableModels.FirstOrDefault();

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
        finally
        {
            _isLoading = false;
        }
    }

    partial void OnSelectedProviderChanged(ProviderInfo? value)
    {
        if (value == null) return;

        AvailableModels.Clear();
        foreach (var model in LlmProviderConfig.Models[value.ProviderId])
            AvailableModels.Add(model);

        SelectedModel = AvailableModels.FirstOrDefault();
        ApiKeyLabel = $"Enter your {value.DisplayName} API key";
        ApiKeyHelpUrl = value.ApiKeyUrl;
        ApiKeyHelpUri = string.IsNullOrEmpty(value.ApiKeyUrl) ? null : new Uri(value.ApiKeyUrl);

        if (!_isLoading)
        {
            ApiKey = string.Empty;
            IsApiKeyConfigured = false;
            MaskedApiKeyDisplay = string.Empty;
        }

        _ = _settingsService.SetProviderAsync(value.ProviderId);
    }

    partial void OnSelectedModelChanged(ModelInfo? value)
    {
        if (value != null)
            _ = _settingsService.SetModelAsync(value.ModelId);
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

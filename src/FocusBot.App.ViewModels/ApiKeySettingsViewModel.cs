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
    private readonly ILlmService _llmService;
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
    private bool _isStatusError;

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
        ILlmService llmService,
        ILogger<ApiKeySettingsViewModel> logger)
    {
        _settingsService = settingsService;
        _llmService = llmService;
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
            IsStatusError = true;
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

        if (!_isLoading)
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
            IsStatusError = true;
            return;
        }

        if (SelectedProvider == null || SelectedModel == null)
        {
            StatusMessage = "Please select a provider and model.";
            IsStatusError = true;
            return;
        }

        IsSaving = true;
        IsStatusError = false;
        StatusMessage = "Checking API key...";

        try
        {
            var validation = await _llmService.ValidateCredentialsAsync(
                ApiKey.Trim(),
                SelectedProvider.ProviderId,
                SelectedModel.ModelId);

            if (validation.ErrorMessage != null)
            {
                StatusMessage = validation.ErrorMessage;
                IsStatusError = true;
                return;
            }

            StatusMessage = "Saving...";
            IsStatusError = false;

            await _settingsService.SetApiKeyAsync(ApiKey);
            await _settingsService.SetProviderAsync(SelectedProvider.ProviderId);
            await _settingsService.SetModelAsync(SelectedModel.ModelId);
            MaskedApiKeyDisplay = "********" + ApiKey[^4..];
            IsApiKeyConfigured = true;
            IsEditing = false;
            ApiKey = string.Empty;
            StatusMessage = "API key saved.";
            IsStatusError = false;

            _logger.LogInformation("API key saved");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving API key");
            StatusMessage = "Error saving API key";
            IsStatusError = true;
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task OpenEditAsync()
    {
        var existingKey = await _settingsService.GetApiKeyAsync();
        ApiKey = existingKey ?? string.Empty;
        IsEditing = true;
        StatusMessage = string.Empty;
        IsStatusError = false;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        ApiKey = string.Empty;
        StatusMessage = string.Empty;
        IsStatusError = false;
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
            IsStatusError = false;

            _logger.LogInformation("API key cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing API key");
            StatusMessage = "Error clearing API key";
            IsStatusError = true;
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

using FocusBot.Core.Entities;

namespace FocusBot.Core.Interfaces;

/// <summary>
/// Service for managing application settings.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets the current API key mode (Own or Managed).
    /// </summary>
    Task<ApiKeyMode> GetApiKeyModeAsync();

    /// <summary>
    /// Sets the API key mode.
    /// </summary>
    Task SetApiKeyModeAsync(ApiKeyMode mode);

    /// <summary>
    /// Gets the encrypted LLM API key.
    /// </summary>
    Task<string?> GetApiKeyAsync();

    /// <summary>
    /// Sets and encrypts the LLM API key.
    /// </summary>
    Task SetApiKeyAsync(string apiKey);

    /// <summary>
    /// Clears the stored API key.
    /// </summary>
    Task ClearApiKeyAsync();

    /// <summary>
    /// Gets the selected LLM provider ID.
    /// </summary>
    Task<string?> GetProviderAsync();

    /// <summary>
    /// Sets the selected LLM provider ID.
    /// </summary>
    Task SetProviderAsync(string provider);

    /// <summary>
    /// Gets the selected LLM model ID.
    /// </summary>
    Task<string?> GetModelAsync();

    /// <summary>
    /// Sets the selected LLM model ID.
    /// </summary>
    Task SetModelAsync(string model);

    /// <summary>
    /// Gets a setting value.
    /// </summary>
    Task<T?> GetSettingAsync<T>(string key);

    /// <summary>
    /// Sets a setting value.
    /// </summary>
    Task SetSettingAsync<T>(string key, T value);
}

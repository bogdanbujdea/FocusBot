namespace FocusBot.Core.Interfaces;

/// <summary>
/// Service for managing application settings.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets the encrypted OpenAI API key.
    /// </summary>
    Task<string?> GetApiKeyAsync();

    /// <summary>
    /// Sets and encrypts the OpenAI API key.
    /// </summary>
    Task SetApiKeyAsync(string apiKey);

    /// <summary>
    /// Clears the stored API key.
    /// </summary>
    Task ClearApiKeyAsync();

    /// <summary>
    /// Gets a setting value.
    /// </summary>
    Task<T?> GetSettingAsync<T>(string key);

    /// <summary>
    /// Sets a setting value.
    /// </summary>
    Task SetSettingAsync<T>(string key, T value);
}

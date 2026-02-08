using System.Text.Json;
using FocusBot.Core.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Service for managing application settings with encrypted storage.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly IDataProtector _protector;
    private readonly ILogger<SettingsService> _logger;
    private readonly string _settingsPath;
    private Dictionary<string, JsonElement> _settings;

    private const string ApiKeySettingName = "OpenAI_ApiKey";

    /// <summary>
    /// Creates the settings service. Use the app data root from ApplicationData for Store/packaged apps.
    /// </summary>
    /// <param name="dataProtectionProvider">Data protection provider for encrypting the API key.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="appDataRoot">Optional root path for settings (e.g. ApplicationData.Current.LocalFolder.Path). If null, uses LocalApplicationData/FocusBot.</param>
    public SettingsService(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<SettingsService> logger,
        string? appDataRoot = null)
    {
        _protector = dataProtectionProvider.CreateProtector("FocusBot.Settings");
        _logger = logger;

        var root = string.IsNullOrWhiteSpace(appDataRoot)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FocusBot")
            : appDataRoot;
        Directory.CreateDirectory(root);
        _settingsPath = Path.Combine(root, "settings.json");

        _settings = LoadSettings();
    }

    public async Task<string?> GetApiKeyAsync()
    {
        try
        {
            var encryptedKey = await GetSettingAsync<string>(ApiKeySettingName);
            if (string.IsNullOrWhiteSpace(encryptedKey))
            {
                return null;
            }

            return _protector.Unprotect(encryptedKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decrypt API key");
            return null;
        }
    }

    public async Task SetApiKeyAsync(string apiKey)
    {
        try
        {
            var encryptedKey = _protector.Protect(apiKey);
            await SetSettingAsync(ApiKeySettingName, encryptedKey);
            _logger.LogInformation("API key saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save API key");
            throw;
        }
    }

    public async Task ClearApiKeyAsync()
    {
        _settings.Remove(ApiKeySettingName);
        await SaveSettingsAsync();
        _logger.LogInformation("API key cleared");
    }

    public Task<T?> GetSettingAsync<T>(string key)
    {
        if (_settings.TryGetValue(key, out var value))
        {
            try
            {
                var result = value.Deserialize<T>();
                return Task.FromResult(result);
            }
            catch
            {
                return Task.FromResult(default(T));
            }
        }

        return Task.FromResult(default(T));
    }

    public async Task SetSettingAsync<T>(string key, T value)
    {
        var jsonElement = JsonSerializer.SerializeToElement(value);
        _settings[key] = jsonElement;
        await SaveSettingsAsync();
    }

    private Dictionary<string, JsonElement> LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
                    ?? new Dictionary<string, JsonElement>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load settings, using defaults");
        }

        return new Dictionary<string, JsonElement>();
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(_settingsPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            throw;
        }
    }
}

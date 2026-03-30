using CSharpFunctionalExtensions;
using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Manages client registration for cloud plan users.
/// Generates a stable client fingerprint on first run and persists the tenant-assigned
/// client ID in local settings.
/// </summary>
public class DesktopClientService(
    IFocusBotApiClient apiClient,
    ISettingsService settings,
    ILogger<DesktopClientService> logger) : IClientService
{
    private const string FingerprintKey = "Client_Fingerprint";
    private const string ClientIdKey = "Client_Id";
    private const string ClientNameKey = "Client_Name";

    private readonly SemaphoreSlim _registerLock = new(1, 1);
    private Guid? _cachedClientId;

    public async Task EnsureClientIdLoadedAsync(CancellationToken ct = default)
    {
        if (_cachedClientId.HasValue)
            return;

        var stored = await settings.GetSettingAsync<string>(ClientIdKey);
        if (Guid.TryParse(stored, out var id))
            _cachedClientId = id;
    }

    public async Task<Result> RegisterAsync(CancellationToken ct = default)
    {
        await _registerLock.WaitAsync(ct);
        try
        {
            if (!apiClient.IsConfigured)
                return Result.Failure("Not authenticated.");

            var fingerprint = await GetOrCreateFingerprintAsync();
            var name = await GetClientNameAsync();

            var response = await apiClient.RegisterClientAsync(
                name,
                fingerprint,
                ClientType.Desktop,
                ClientHost.Windows);
            if (response is null)
                return Result.Failure("Client registration request failed.");

            _cachedClientId = response.Id;
            await settings.SetSettingAsync(ClientIdKey, response.Id.ToString());

            logger.LogInformation("Client registered with ID {ClientId}", response.Id);
            return Result.Success();
        }
        finally
        {
            _registerLock.Release();
        }
    }

    public async Task DeregisterAsync(CancellationToken ct = default)
    {
        var clientId = await GetOrLoadClientIdAsync();
        if (clientId is null)
            return;

        await apiClient.DeregisterClientAsync(clientId.Value);

        _cachedClientId = null;
        await settings.SetSettingAsync<string?>(ClientIdKey, null);

        logger.LogInformation("Client {ClientId} deregistered", clientId);
    }

    public Guid? GetClientId() => _cachedClientId;

    private async Task<Guid?> GetOrLoadClientIdAsync()
    {
        if (_cachedClientId.HasValue)
            return _cachedClientId;

        var stored = await settings.GetSettingAsync<string>(ClientIdKey);
        if (Guid.TryParse(stored, out var id))
        {
            _cachedClientId = id;
            return id;
        }

        return null;
    }

    private async Task<string> GetOrCreateFingerprintAsync()
    {
        var existing = await settings.GetSettingAsync<string>(FingerprintKey);
        if (!string.IsNullOrWhiteSpace(existing))
            return existing;

        var newFingerprint = Guid.NewGuid().ToString("N");
        await settings.SetSettingAsync(FingerprintKey, newFingerprint);
        logger.LogInformation("Generated new client fingerprint");
        return newFingerprint;
    }

    private async Task<string> GetClientNameAsync()
    {
        var stored = await settings.GetSettingAsync<string>(ClientNameKey);
        if (!string.IsNullOrWhiteSpace(stored))
            return stored;

        return Environment.MachineName;
    }
}

using CSharpFunctionalExtensions;
using FocusBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Manages device registration and heartbeat for cloud plan users.
/// Generates a stable device fingerprint on first run and persists the tenant-assigned
/// device ID in local settings.
/// </summary>
public class DesktopDeviceService(
    IFocusBotApiClient apiClient,
    ISettingsService settings,
    ILogger<DesktopDeviceService> logger) : IDeviceService
{
    private const string FingerprintKey = "Device_Fingerprint";
    private const string DeviceIdKey = "Device_Id";
    private const string DeviceNameKey = "Device_Name";

    private Guid? _cachedDeviceId;

    public async Task<Result> RegisterAsync(CancellationToken ct = default)
    {
        if (!apiClient.IsConfigured)
            return Result.Failure("Not authenticated.");

        var fingerprint = await GetOrCreateFingerprintAsync();
        var name = await GetDeviceNameAsync();

        var response = await apiClient.RegisterDeviceAsync(name, fingerprint);
        if (response is null)
            return Result.Failure("Device registration request failed.");

        _cachedDeviceId = response.Id;
        await settings.SetSettingAsync(DeviceIdKey, response.Id.ToString());

        logger.LogInformation("Device registered with ID {DeviceId}", response.Id);
        return Result.Success();
    }

    public async Task SendHeartbeatAsync(CancellationToken ct = default)
    {
        var deviceId = await GetOrLoadDeviceIdAsync();
        if (deviceId is null)
        {
            logger.LogDebug("No device ID available; skipping heartbeat");
            return;
        }

        var success = await apiClient.SendHeartbeatAsync(deviceId.Value);
        if (!success)
        {
            logger.LogWarning("Heartbeat failed for device {DeviceId}; will re-register on next attempt", deviceId);
            _cachedDeviceId = null;
            await settings.SetSettingAsync<string?>(DeviceIdKey, null);
        }
    }

    public async Task DeregisterAsync(CancellationToken ct = default)
    {
        var deviceId = await GetOrLoadDeviceIdAsync();
        if (deviceId is null)
            return;

        await apiClient.DeregisterDeviceAsync(deviceId.Value);

        _cachedDeviceId = null;
        await settings.SetSettingAsync<string?>(DeviceIdKey, null);

        logger.LogInformation("Device {DeviceId} deregistered", deviceId);
    }

    public Guid? GetDeviceId() => _cachedDeviceId;

    private async Task<Guid?> GetOrLoadDeviceIdAsync()
    {
        if (_cachedDeviceId.HasValue)
            return _cachedDeviceId;

        var stored = await settings.GetSettingAsync<string>(DeviceIdKey);
        if (Guid.TryParse(stored, out var id))
        {
            _cachedDeviceId = id;
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
        logger.LogInformation("Generated new device fingerprint");
        return newFingerprint;
    }

    private async Task<string> GetDeviceNameAsync()
    {
        var stored = await settings.GetSettingAsync<string>(DeviceNameKey);
        if (!string.IsNullOrWhiteSpace(stored))
            return stored;

        return Environment.MachineName;
    }
}

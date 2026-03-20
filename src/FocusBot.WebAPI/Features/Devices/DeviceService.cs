using FocusBot.WebAPI.Data;
using FocusBot.WebAPI.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FocusBot.WebAPI.Features.Devices;

/// <summary>
/// Business logic for device registration, heartbeat, and lifecycle management.
/// A device is considered online if its LastSeenAtUtc is within the online threshold.
/// </summary>
public class DeviceService(ApiDbContext db)
{
    private static readonly TimeSpan OnlineThreshold = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Registers a new device for the user or updates an existing registration
    /// (matched by fingerprint) when the same device re-registers after reinstall.
    /// </summary>
    public async Task<DeviceResponse> RegisterAsync(
        Guid userId, RegisterDeviceRequest request, CancellationToken ct = default)
    {
        var existing = await db.Devices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.Fingerprint == request.Fingerprint, ct);

        if (existing is not null)
        {
            existing.Name = request.Name;
            existing.DeviceType = request.DeviceType;
            existing.AppVersion = request.AppVersion;
            existing.Platform = request.Platform;
            existing.LastSeenAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return ToResponse(existing);
        }

        var device = new Device
        {
            UserId = userId,
            DeviceType = request.DeviceType,
            Name = request.Name,
            Fingerprint = request.Fingerprint,
            AppVersion = request.AppVersion,
            Platform = request.Platform,
            LastSeenAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
        };

        db.Devices.Add(device);
        await db.SaveChangesAsync(ct);

        return ToResponse(device);
    }

    /// <summary>Returns all devices registered to the user.</summary>
    public async Task<IReadOnlyList<DeviceResponse>> GetDevicesAsync(
        Guid userId, CancellationToken ct = default)
    {
        var devices = await db.Devices
            .AsNoTracking()
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.LastSeenAtUtc)
            .ToListAsync(ct);

        return devices.Select(ToResponse).ToList();
    }

    /// <summary>
    /// Updates LastSeenAtUtc and optional version/platform fields.
    /// Returns null if the device is not found or does not belong to the user.
    /// </summary>
    public async Task<DeviceResponse?> HeartbeatAsync(
        Guid userId, Guid deviceId, HeartbeatRequest request, CancellationToken ct = default)
    {
        var device = await db.Devices
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.UserId == userId, ct);

        if (device is null)
            return null;

        device.LastSeenAtUtc = DateTime.UtcNow;

        if (request.AppVersion is not null)
            device.AppVersion = request.AppVersion;

        if (request.Platform is not null)
            device.Platform = request.Platform;

        await db.SaveChangesAsync(ct);

        return ToResponse(device);
    }

    /// <summary>
    /// Deletes a device registration. Returns false if not found or not owned by the user.
    /// </summary>
    public async Task<bool> DeleteAsync(Guid userId, Guid deviceId, CancellationToken ct = default)
    {
        var device = await db.Devices
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.UserId == userId, ct);

        if (device is null)
            return false;

        db.Devices.Remove(device);
        await db.SaveChangesAsync(ct);

        return true;
    }

    private DeviceResponse ToResponse(Device d) =>
        new(d.Id, d.DeviceType, d.Name, d.Fingerprint, d.AppVersion, d.Platform,
            d.LastSeenAtUtc, d.CreatedAtUtc,
            IsOnline: DateTime.UtcNow - d.LastSeenAtUtc < OnlineThreshold);
}

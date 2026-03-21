using FocusBot.WebAPI.Data.Entities;

namespace FocusBot.WebAPI.Features.Devices;

/// <summary>Request body for registering a new device.</summary>
public sealed record RegisterDeviceRequest(
    DeviceType DeviceType,
    string Name,
    string Fingerprint,
    string? AppVersion,
    string? Platform);

/// <summary>Request body for sending a heartbeat from a device.</summary>
public sealed record HeartbeatRequest(string? AppVersion, string? Platform);

/// <summary>Response DTO for a registered device.</summary>
public sealed record DeviceResponse(
    Guid Id,
    DeviceType DeviceType,
    string Name,
    string Fingerprint,
    string? AppVersion,
    string? Platform,
    DateTime LastSeenAtUtc,
    DateTime CreatedAtUtc,
    bool IsOnline);

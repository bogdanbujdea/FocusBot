using FocusBot.WebAPI.Data.Entities;

namespace FocusBot.WebAPI.Features.Clients;

/// <summary>Request body for registering a new client.</summary>
public sealed record RegisterClientRequest(
    ClientType ClientType,
    ClientHost Host,
    string Name,
    string Fingerprint,
    string? AppVersion,
    string? Platform);

/// <summary>Request body for sending a heartbeat from a client.</summary>
public sealed record HeartbeatRequest(string? AppVersion, string? Platform);

/// <summary>Response DTO for a registered client.</summary>
public sealed record ClientResponse(
    Guid Id,
    ClientType ClientType,
    ClientHost Host,
    string Name,
    string Fingerprint,
    string? AppVersion,
    string? Platform,
    string? IpAddress,
    DateTime LastSeenAtUtc,
    DateTime CreatedAtUtc,
    bool IsOnline);

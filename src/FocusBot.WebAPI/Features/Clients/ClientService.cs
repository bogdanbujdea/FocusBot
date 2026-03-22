using FocusBot.WebAPI.Data;
using FocusBot.WebAPI.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FocusBot.WebAPI.Features.Clients;

/// <summary>
/// Business logic for client registration, heartbeat, and lifecycle management.
/// A client is considered online if its LastSeenAtUtc is within the online threshold.
/// </summary>
public class ClientService(ApiDbContext db)
{
    private static readonly TimeSpan OnlineThreshold = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Registers a new client for the user or updates an existing registration
    /// (matched by fingerprint) when the same client re-registers after reinstall.
    /// </summary>
    public async Task<ClientResponse> RegisterAsync(
        Guid userId,
        RegisterClientRequest request,
        string? remoteIpAddress,
        CancellationToken ct = default)
    {
        var existing = await db.Clients
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Fingerprint == request.Fingerprint, ct);

        if (existing is not null)
        {
            ApplyRegistrationUpdate(existing, request, remoteIpAddress);
            await db.SaveChangesAsync(ct);
            return ToResponse(existing);
        }

        var client = new Client
        {
            UserId = userId,
            ClientType = request.ClientType,
            Host = request.Host,
            Name = request.Name,
            Fingerprint = request.Fingerprint,
            AppVersion = request.AppVersion,
            Platform = request.Platform,
            IpAddress = remoteIpAddress,
            LastSeenAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
        };

        db.Clients.Add(client);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "23505")
        {
            var race = await db.Clients
                .FirstOrDefaultAsync(c => c.UserId == userId && c.Fingerprint == request.Fingerprint, ct);
            if (race is null)
                throw;

            ApplyRegistrationUpdate(race, request, remoteIpAddress);
            await db.SaveChangesAsync(ct);
            return ToResponse(race);
        }

        return ToResponse(client);
    }

    private static void ApplyRegistrationUpdate(
        Client target,
        RegisterClientRequest request,
        string? remoteIpAddress)
    {
        target.Name = request.Name;
        target.ClientType = request.ClientType;
        target.Host = request.Host;
        target.AppVersion = request.AppVersion;
        target.Platform = request.Platform;
        target.IpAddress = remoteIpAddress;
        target.LastSeenAtUtc = DateTime.UtcNow;
    }

    /// <summary>Returns all clients registered to the user.</summary>
    public async Task<IReadOnlyList<ClientResponse>> GetClientsAsync(
        Guid userId, CancellationToken ct = default)
    {
        var clients = await db.Clients
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.LastSeenAtUtc)
            .ToListAsync(ct);

        return clients.Select(ToResponse).ToList();
    }

    /// <summary>
    /// Updates LastSeenAtUtc and optional version/platform fields.
    /// Returns null if the client is not found or does not belong to the user.
    /// </summary>
    public async Task<ClientResponse?> HeartbeatAsync(
        Guid userId,
        Guid clientId,
        HeartbeatRequest request,
        string? remoteIpAddress,
        CancellationToken ct = default)
    {
        var client = await db.Clients
            .FirstOrDefaultAsync(c => c.Id == clientId && c.UserId == userId, ct);

        if (client is null)
            return null;

        client.LastSeenAtUtc = DateTime.UtcNow;
        client.IpAddress = remoteIpAddress;

        if (request.AppVersion is not null)
            client.AppVersion = request.AppVersion;

        if (request.Platform is not null)
            client.Platform = request.Platform;

        await db.SaveChangesAsync(ct);

        return ToResponse(client);
    }

    /// <summary>
    /// Deletes a client registration. Returns false if not found or not owned by the user.
    /// </summary>
    public async Task<bool> DeleteAsync(Guid userId, Guid clientId, CancellationToken ct = default)
    {
        var client = await db.Clients
            .FirstOrDefaultAsync(c => c.Id == clientId && c.UserId == userId, ct);

        if (client is null)
            return false;

        db.Clients.Remove(client);
        await db.SaveChangesAsync(ct);

        return true;
    }

    private ClientResponse ToResponse(Client c) =>
        new(
            c.Id,
            c.ClientType,
            c.Host,
            c.Name,
            c.Fingerprint,
            c.AppVersion,
            c.Platform,
            c.IpAddress,
            c.LastSeenAtUtc,
            c.CreatedAtUtc,
            IsOnline: DateTime.UtcNow - c.LastSeenAtUtc < OnlineThreshold);
}

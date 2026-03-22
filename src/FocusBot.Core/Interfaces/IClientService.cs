using CSharpFunctionalExtensions;

namespace FocusBot.Core.Interfaces;

/// <summary>
/// Manages client registration and heartbeat for cloud plan users.
/// </summary>
public interface IClientService
{
    /// <summary>
    /// Loads the persisted server-assigned client id from settings into memory if not already cached.
    /// </summary>
    Task EnsureClientIdLoadedAsync(CancellationToken ct = default);

    /// <summary>
    /// Registers this client with the backend (or updates the registration if already registered).
    /// Stores the returned client ID locally for use in session attribution and heartbeats.
    /// </summary>
    Task<Result> RegisterAsync(CancellationToken ct = default);

    /// <summary>
    /// Sends a heartbeat to the backend to mark the client as online.
    /// On 401, the HTTP client retries once after refreshing the access token.
    /// On 404, the client is re-registered automatically.
    /// </summary>
    Task SendHeartbeatAsync(CancellationToken ct = default);

    /// <summary>
    /// Removes the client registration from the backend (called on explicit logout).
    /// </summary>
    Task DeregisterAsync(CancellationToken ct = default);

    /// <summary>Returns the locally stored client ID, or null if not yet registered.</summary>
    Guid? GetClientId();
}

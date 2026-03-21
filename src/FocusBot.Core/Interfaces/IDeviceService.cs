using CSharpFunctionalExtensions;

namespace FocusBot.Core.Interfaces;

/// <summary>
/// Manages device registration and heartbeat for cloud plan users.
/// </summary>
public interface IDeviceService
{
    /// <summary>
    /// Registers this device with the backend (or updates the registration if already registered).
    /// Stores the returned device ID locally for use in session attribution and heartbeats.
    /// </summary>
    Task<Result> RegisterAsync(CancellationToken ct = default);

    /// <summary>
    /// Sends a heartbeat to the backend to mark the device as online.
    /// On 401, the HTTP client retries once after refreshing the access token.
    /// On 404, the device is re-registered automatically.
    /// </summary>
    Task SendHeartbeatAsync(CancellationToken ct = default);

    /// <summary>
    /// Removes the device registration from the backend (called on explicit logout).
    /// </summary>
    Task DeregisterAsync(CancellationToken ct = default);

    /// <summary>Returns the locally stored device ID, or null if not yet registered.</summary>
    Guid? GetDeviceId();
}

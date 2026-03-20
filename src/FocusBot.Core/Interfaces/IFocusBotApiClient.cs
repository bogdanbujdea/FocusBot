using FocusBot.Core.Entities;

namespace FocusBot.Core.Interfaces;

/// <summary>
/// Typed HTTP client for the FocusBot Web API.
/// </summary>
public interface IFocusBotApiClient
{
    /// <summary>Whether the client has an authenticated user session.</summary>
    bool IsConfigured { get; }

    // Sessions
    Task<ApiSessionResponse?> StartSessionAsync(StartSessionPayload payload);
    Task<ApiSessionResponse?> EndSessionAsync(Guid sessionId, EndSessionPayload payload);
    /// <summary>Gets the currently active focus session for the authenticated user, or null if none exists.</summary>
    Task<ApiSessionResponse?> GetActiveSessionAsync();

    // Classification
    /// <summary>Classify the current window context. Pass <paramref name="byokApiKey"/> for BYOK users.</summary>
    Task<ApiClassifyResponse?> ClassifyAsync(ClassifyPayload payload, string? byokApiKey = null);
    Task<ApiValidateKeyResponse?> ValidateKeyAsync(ValidateKeyPayload payload);

    // Subscriptions
    Task<ApiSubscriptionStatus?> GetSubscriptionStatusAsync();

    // Devices
    Task<ApiDeviceResponse?> RegisterDeviceAsync(string name, string fingerprint);
    Task<bool> SendHeartbeatAsync(Guid deviceId);
    Task<bool> DeregisterDeviceAsync(Guid deviceId);
}

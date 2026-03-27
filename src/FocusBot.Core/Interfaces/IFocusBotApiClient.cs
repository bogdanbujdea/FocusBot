using FocusBot.Core.Entities;
using System.Net;


namespace FocusBot.Core.Interfaces;

/// <summary>
/// Typed HTTP client for the FocusBot Web API.
/// </summary>
public interface IFocusBotApiClient
{
    /// <summary>Whether the client has an authenticated user session.</summary>
    bool IsConfigured { get; }

    // Sessions
    Task<ApiResult<ApiSessionResponse>> StartSessionAsync(StartSessionPayload payload);
    Task<ApiResult<ApiSessionResponse>> EndSessionAsync(Guid sessionId, EndSessionPayload payload);
    Task<ApiResult<ApiSessionResponse>> PauseSessionAsync(Guid sessionId);
    Task<ApiResult<ApiSessionResponse>> ResumeSessionAsync(Guid sessionId);
    /// <summary>Gets the currently active focus session for the authenticated user, or null if none exists.</summary>
    Task<ApiSessionResponse?> GetActiveSessionAsync();

    // Classification
    /// <summary>Classify the current window context. Pass <paramref name="byokApiKey"/> for BYOK users.</summary>
    Task<ApiClassifyResponse?> ClassifyAsync(ClassifyPayload payload, string? byokApiKey = null);
    Task<ApiValidateKeyResponse?> ValidateKeyAsync(ValidateKeyPayload payload);

    // Subscriptions
    Task<ApiSubscriptionStatus?> GetSubscriptionStatusAsync();

    // Clients
    Task<ApiClientResponse?> RegisterClientAsync(
        string name,
        string fingerprint,
        ClientType clientType = ClientType.Desktop,
        ClientHost host = ClientHost.Windows);
    /// <summary>
    /// Sends a heartbeat PUT. Returns the HTTP status code, or null on a network/exception failure.
    /// Automatically retries once after a token refresh on 401.
    /// </summary>
    Task<HttpStatusCode?> SendHeartbeatAsync(Guid clientId);
    Task<bool> DeregisterClientAsync(Guid clientId);

    // Account
    /// <summary>
    /// Calls GET /auth/me to ensure the authenticated user's account is provisioned in the
    /// backend database. Safe to call on every sign-in; the backend is idempotent.
    /// Returns true when the call succeeds.
    /// </summary>
    Task<bool> ProvisionUserAsync();
}

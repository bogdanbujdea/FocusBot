namespace FocusBot.Core.Interfaces;

/// <summary>
/// Provides Supabase authentication operations including sign-in, token management, and session lifecycle.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Sends a magic link to the specified email address via Supabase OTP.
    /// </summary>
    Task<bool> SignInWithMagicLinkAsync(string email);

    /// <summary>
    /// Handles the authentication callback URI, extracting tokens from the fragment or exchanging an auth code.
    /// </summary>
    Task<bool> HandleCallbackAsync(string uri);

    /// <summary>
    /// Returns the current access token, proactively refreshing it if near expiry.
    /// </summary>
    Task<string?> GetAccessTokenAsync();

    /// <summary>
    /// Signs out the current user, clearing stored tokens and notifying the Supabase server.
    /// </summary>
    Task SignOutAsync();

    /// <summary>
    /// Attempts to restore a previous session from stored tokens on application startup.
    /// Refreshes the access token if it has expired.
    /// </summary>
    Task TryRestoreSessionAsync();

    /// <summary>
    /// Refreshes the access token using the stored refresh token.
    /// </summary>
    Task<bool> RefreshTokenAsync();

    /// <summary>
    /// Gets a value indicating whether the user is currently authenticated.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Gets the email address of the currently authenticated user, or null if not signed in.
    /// </summary>
    Task<string?> GetUserEmailAsync();

    /// <summary>
    /// Raised when the authentication state changes (sign-in, sign-out, or token refresh).
    /// </summary>
    event Action? AuthStateChanged;

    /// <summary>
    /// Raised when the refresh token is exhausted and the user must sign in again.
    /// </summary>
    event Action? ReAuthRequired;
}

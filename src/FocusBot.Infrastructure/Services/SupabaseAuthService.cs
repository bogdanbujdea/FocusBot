using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FocusBot.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Supabase;
using Supabase.Gotrue;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Supabase configuration values used for authentication API calls.
/// </summary>
internal sealed record SupabaseConfig(string Url, string PublishableKey);

/// <summary>
/// Implements Supabase authentication using direct HTTP calls (no supabase-csharp dependency).
/// Tokens are stored via <see cref="ISettingsService"/> and proactively refreshed before expiry.
/// </summary>
public class SupabaseAuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<SupabaseAuthService> _logger;
    private Supabase.Client? _supabaseClient;

    private string? _accessToken;
    private string? _refreshToken;
    private DateTime _expiresAtUtc = DateTime.MinValue;
    private bool _isAuthenticated;

    private const string SupabaseUrlKey = "Supabase_Url";
    private const string SupabasePublishableKeyKey = "Supabase_PublishableKey";
    private const string StoredAccessTokenKey = "Auth_AccessToken";
    private const string StoredRefreshTokenKey = "Auth_RefreshToken";
    private const string StoredExpiresAtKey = "Auth_ExpiresAtUtc";

    // Keep these in sync with browser-extension/src/shared/supabaseClient.ts and WebAPI host.
    private const string DefaultSupabaseUrl = "https://mokjfxtnqmudypnukqsv.supabase.co";
    private const string DefaultSupabasePublishableKey =
        "sb_publishable_U9dKqMzxtpms_EGvvUybCg_IKGoPc3t";
#if DEBUG
    private const string DefaultMagicLinkRedirectTo =
        "http://localhost:5251/auth/callback.html?source=desktop";
#else
    private const string DefaultMagicLinkRedirectTo =
        "https://api.foqus.me/auth/callback.html?source=desktop";
#endif

    /// <summary>
    /// The buffer time before token expiry at which a proactive refresh is triggered.
    /// Five minutes gives enough headroom to retry across transient network failures.
    /// </summary>
    private static readonly TimeSpan RefreshBuffer = TimeSpan.FromMinutes(5);

    /// <summary>Maximum number of refresh attempts before giving up and requesting re-login.</summary>
    private const int MaxRefreshAttempts = 3;

    /// <inheritdoc />
    public bool IsAuthenticated => _isAuthenticated;

    /// <inheritdoc />
    public event Action? AuthStateChanged;

    /// <inheritdoc />
    public event Action? ReAuthRequired;

    /// <summary>
    /// Initializes a new instance of the <see cref="SupabaseAuthService"/> class.
    /// </summary>
    public SupabaseAuthService(
        HttpClient httpClient,
        ISettingsService settingsService,
        ILogger<SupabaseAuthService> logger
    )
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> SignInWithMagicLinkAsync(string email)
    {
        try
        {
            var client = await GetSupabaseClientAsync();

            var options = new SignInOptions
            {
                RedirectTo = DefaultMagicLinkRedirectTo
            };

            var didSendMagicLink = await client.Auth.SendMagicLink(email, options);
            if (!didSendMagicLink)
            {
                _logger.LogWarning("Supabase SendMagicLink returned false for {Email}", email);
            }
            else
            {
                _logger.LogInformation("Magic link sent to {Email}", email);
            }

            return didSendMagicLink;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send magic link");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> HandleCallbackAsync(string uri)
    {
        try
        {
            var parsed = new Uri(uri);
            var fragment = parsed.Fragment.TrimStart('#');
            var query = System.Web.HttpUtility.ParseQueryString(fragment);

            var accessToken = query["access_token"];
            var refreshToken = query["refresh_token"];
            var expiresInRaw = query["expires_in"];

            if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(refreshToken))
            {
                var expiresIn = int.TryParse(expiresInRaw, out var parsed_ei) ? parsed_ei : 3600;
                await StoreTokensAsync(accessToken, refreshToken, expiresIn);
                return true;
            }

            var code =
                query["code"] ?? System.Web.HttpUtility.ParseQueryString(parsed.Query)["code"];

            if (string.IsNullOrEmpty(code))
            {
                _logger.LogWarning("Callback URI contains neither tokens nor a code");
                return false;
            }

            var config = await GetSupabaseConfigAsync();
            if (config is null)
                return false;

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{config.Url}/auth/v1/token?grant_type=pkce"
            );
            request.Headers.Add("apikey", config.PublishableKey);
            request.Content = JsonContent.Create(new { code });

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning(
                    "Token exchange failed: {StatusCode} {Body}",
                    response.StatusCode,
                    body
                );
                return false;
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<SupabaseTokenResponse>();
            if (
                tokenResponse is null
                || string.IsNullOrEmpty(tokenResponse.AccessToken)
                || string.IsNullOrEmpty(tokenResponse.RefreshToken)
            )
            {
                _logger.LogWarning("Token response missing access or refresh token");
                return false;
            }

            await StoreTokensAsync(
                tokenResponse.AccessToken,
                tokenResponse.RefreshToken,
                tokenResponse.ExpiresIn
            );
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle auth callback");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<string?> GetAccessTokenAsync()
    {
        if (_accessToken is null)
            return null;

        if (DateTime.UtcNow >= _expiresAtUtc - RefreshBuffer)
        {
            _logger.LogDebug("Access token near expiry, proactively refreshing");
            var refreshed = await RefreshTokenAsync();
            if (!refreshed)
            {
                _logger.LogWarning("Proactive token refresh failed after {MaxAttempts} attempts; re-auth required", MaxRefreshAttempts);
                ReAuthRequired?.Invoke();
                return null;
            }
        }

        return _accessToken;
    }

    /// <inheritdoc />
    public async Task SignOutAsync()
    {
        try
        {
            if (!string.IsNullOrEmpty(_accessToken))
            {
                var config = await GetSupabaseConfigAsync();
                if (config is not null)
                {
                    using var request = new HttpRequestMessage(
                        HttpMethod.Post,
                        $"{config.Url}/auth/v1/logout"
                    );
                    request.Headers.Add("apikey", config.PublishableKey);
                    request.Headers.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue(
                            "Bearer",
                            _accessToken
                        );

                    try
                    {
                        await _httpClient.SendAsync(request);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Server-side sign-out failed (non-critical)");
                    }
                }
            }
        }
        finally
        {
            _accessToken = null;
            _refreshToken = null;
            _expiresAtUtc = DateTime.MinValue;
            _isAuthenticated = false;

            await _settingsService.SetSettingAsync<string?>(StoredAccessTokenKey, null);
            await _settingsService.SetSettingAsync<string?>(StoredRefreshTokenKey, null);
            await _settingsService.SetSettingAsync<string?>(StoredExpiresAtKey, null);

            AuthStateChanged?.Invoke();
        }
    }

    /// <inheritdoc />
    public async Task TryRestoreSessionAsync()
    {
        try
        {
            var storedAccess = await _settingsService.GetSettingAsync<string>(StoredAccessTokenKey);
            var storedRefresh = await _settingsService.GetSettingAsync<string>(
                StoredRefreshTokenKey
            );
            var storedExpiry = await _settingsService.GetSettingAsync<string>(StoredExpiresAtKey);

            if (string.IsNullOrEmpty(storedAccess) || string.IsNullOrEmpty(storedRefresh))
            {
                _logger.LogDebug("No stored auth session to restore");
                return;
            }

            _accessToken = storedAccess;
            _refreshToken = storedRefresh;

            if (
                DateTime.TryParse(
                    storedExpiry,
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var expiry
                )
            )
            {
                _expiresAtUtc = expiry;
            }

            if (DateTime.UtcNow >= _expiresAtUtc - RefreshBuffer)
            {
                _logger.LogInformation("Stored access token expired or near expiry, refreshing");
                var refreshed = await RefreshTokenAsync();
                if (!refreshed)
                {
                    _logger.LogWarning("Session restore failed: could not refresh token");
                    await ClearStoredTokensAsync();
                    return;
                }
            }

            _isAuthenticated = true;
            AuthStateChanged?.Invoke();
            _logger.LogInformation("Auth session restored successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore auth session");
        }
    }

    /// <inheritdoc />
    public async Task<bool> RefreshTokenAsync()
    {
        if (string.IsNullOrEmpty(_refreshToken))
        {
            _logger.LogWarning("No refresh token available");
            return false;
        }

        for (var attempt = 1; attempt <= MaxRefreshAttempts; attempt++)
        {
            try
            {
                var config = await GetSupabaseConfigAsync();
                if (config is null)
                    return false;

                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{config.Url}/auth/v1/token?grant_type=refresh_token"
                );
                request.Headers.Add("apikey", config.PublishableKey);
                request.Content = JsonContent.Create(new { refresh_token = _refreshToken });

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning(
                        "Token refresh attempt {Attempt}/{Max} failed: {StatusCode} {Body}",
                        attempt,
                        MaxRefreshAttempts,
                        response.StatusCode,
                        body
                    );

                    // 4xx errors (e.g. 400 invalid_grant) are not retryable
                    if ((int)response.StatusCode is >= 400 and < 500)
                        return false;

                    if (attempt < MaxRefreshAttempts)
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));

                    continue;
                }

                var tokenResponse = await response.Content.ReadFromJsonAsync<SupabaseTokenResponse>();
                if (
                    tokenResponse is null
                    || string.IsNullOrEmpty(tokenResponse.AccessToken)
                    || string.IsNullOrEmpty(tokenResponse.RefreshToken)
                )
                {
                    _logger.LogWarning("Refresh response missing tokens on attempt {Attempt}", attempt);
                    return false;
                }

                await StoreTokensAsync(
                    tokenResponse.AccessToken,
                    tokenResponse.RefreshToken,
                    tokenResponse.ExpiresIn
                );

                _logger.LogInformation("Access token refreshed successfully on attempt {Attempt}", attempt);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token refresh attempt {Attempt}/{Max} threw an exception", attempt, MaxRefreshAttempts);

                if (attempt < MaxRefreshAttempts)
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            }
        }

        _logger.LogError("All {Max} token refresh attempts failed", MaxRefreshAttempts);
        return false;
    }

    private async Task StoreTokensAsync(
        string accessToken,
        string refreshToken,
        int expiresInSeconds
    )
    {
        _accessToken = accessToken;
        _refreshToken = refreshToken;
        _expiresAtUtc = DateTime.UtcNow.AddSeconds(expiresInSeconds);
        _isAuthenticated = true;

        await _settingsService.SetSettingAsync(StoredAccessTokenKey, accessToken);
        await _settingsService.SetSettingAsync(StoredRefreshTokenKey, refreshToken);
        await _settingsService.SetSettingAsync(StoredExpiresAtKey, _expiresAtUtc.ToString("O"));

        AuthStateChanged?.Invoke();
    }

    private async Task ClearStoredTokensAsync()
    {
        _accessToken = null;
        _refreshToken = null;
        _expiresAtUtc = DateTime.MinValue;
        _isAuthenticated = false;

        await _settingsService.SetSettingAsync<string?>(StoredAccessTokenKey, null);
        await _settingsService.SetSettingAsync<string?>(StoredRefreshTokenKey, null);
        await _settingsService.SetSettingAsync<string?>(StoredExpiresAtKey, null);
    }

    private async Task<SupabaseConfig?> GetSupabaseConfigAsync()
    {
        var url = await _settingsService.GetSettingAsync<string>(SupabaseUrlKey);
        var publishableKey = await _settingsService.GetSettingAsync<string>(
            SupabasePublishableKeyKey
        );

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(publishableKey))
        {
            _logger.LogInformation(
                "Supabase URL or publishable key not configured; using built-in defaults"
            );
            return new SupabaseConfig(DefaultSupabaseUrl, DefaultSupabasePublishableKey);
        }

        return new SupabaseConfig(url, publishableKey);
    }

    private async Task<Supabase.Client> GetSupabaseClientAsync()
    {
        if (_supabaseClient is not null)
            return _supabaseClient;

        var config = await GetSupabaseConfigAsync();
        var options = new SupabaseOptions { AutoConnectRealtime = false };

        _supabaseClient = new Supabase.Client(config!.Url, config.PublishableKey, options);
        await _supabaseClient.InitializeAsync();
        return _supabaseClient;
    }

    private sealed class SupabaseTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }
    }
}

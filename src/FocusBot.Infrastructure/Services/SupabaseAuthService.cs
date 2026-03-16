using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FocusBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Implements Supabase authentication using direct HTTP calls (no supabase-csharp dependency).
/// Tokens are stored via ISettingsService.
/// </summary>
public class SupabaseAuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<SupabaseAuthService> _logger;

    private string? _accessToken;
    private string? _refreshToken;
    private bool _isAuthenticated;

    private const string SupabaseUrlKey = "Supabase_Url";
    private const string SupabaseAnonKeyKey = "Supabase_AnonKey";
    private const string StoredAccessTokenKey = "Auth_AccessToken";
    private const string StoredRefreshTokenKey = "Auth_RefreshToken";

    public bool IsAuthenticated => _isAuthenticated;
    public event Action? AuthStateChanged;

    public SupabaseAuthService(
        HttpClient httpClient,
        ISettingsService settingsService,
        ILogger<SupabaseAuthService> logger)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<bool> SignInWithMagicLinkAsync(string email)
    {
        try
        {
            var (url, anonKey) = await GetSupabaseConfigAsync();
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(anonKey))
            {
                _logger.LogWarning("Supabase URL or anon key not configured");
                return false;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{url}/auth/v1/otp");
            request.Headers.Add("apikey", anonKey);
            request.Content = JsonContent.Create(new { email });

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Magic link request failed: {StatusCode} {Body}", response.StatusCode, body);
                return false;
            }

            _logger.LogInformation("Magic link sent to {Email}", email);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send magic link");
            return false;
        }
    }

    public async Task<bool> HandleCallbackAsync(string uri)
    {
        try
        {
            var parsed = new Uri(uri);
            var fragment = parsed.Fragment.TrimStart('#');
            var query = System.Web.HttpUtility.ParseQueryString(fragment);

            var accessToken = query["access_token"];
            var refreshToken = query["refresh_token"];

            if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(refreshToken))
            {
                await StoreTokensAsync(accessToken, refreshToken);
                return true;
            }

            var code = query["code"]
                       ?? System.Web.HttpUtility.ParseQueryString(parsed.Query)["code"];

            if (string.IsNullOrEmpty(code))
            {
                _logger.LogWarning("Callback URI contains neither tokens nor a code");
                return false;
            }

            var (url, anonKey) = await GetSupabaseConfigAsync();
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(anonKey))
                return false;

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{url}/auth/v1/token?grant_type=pkce");
            request.Headers.Add("apikey", anonKey);
            request.Content = JsonContent.Create(new { code });

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Token exchange failed: {StatusCode} {Body}", response.StatusCode, body);
                return false;
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<SupabaseTokenResponse>();
            if (tokenResponse is null
                || string.IsNullOrEmpty(tokenResponse.AccessToken)
                || string.IsNullOrEmpty(tokenResponse.RefreshToken))
            {
                _logger.LogWarning("Token response missing access or refresh token");
                return false;
            }

            await StoreTokensAsync(tokenResponse.AccessToken, tokenResponse.RefreshToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle auth callback");
            return false;
        }
    }

    public Task<string?> GetAccessTokenAsync()
    {
        return Task.FromResult(_accessToken);
    }

    public async Task SignOutAsync()
    {
        try
        {
            if (!string.IsNullOrEmpty(_accessToken))
            {
                var (url, anonKey) = await GetSupabaseConfigAsync();
                if (!string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(anonKey))
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, $"{url}/auth/v1/logout");
                    request.Headers.Add("apikey", anonKey);
                    request.Headers.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

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
            _isAuthenticated = false;

            await _settingsService.SetSettingAsync<string?>(StoredAccessTokenKey, null);
            await _settingsService.SetSettingAsync<string?>(StoredRefreshTokenKey, null);

            AuthStateChanged?.Invoke();
        }
    }

    private async Task StoreTokensAsync(string accessToken, string refreshToken)
    {
        _accessToken = accessToken;
        _refreshToken = refreshToken;
        _isAuthenticated = true;

        await _settingsService.SetSettingAsync(StoredAccessTokenKey, accessToken);
        await _settingsService.SetSettingAsync(StoredRefreshTokenKey, refreshToken);

        AuthStateChanged?.Invoke();
    }

    private async Task<(string? Url, string? AnonKey)> GetSupabaseConfigAsync()
    {
        var url = await _settingsService.GetSettingAsync<string>(SupabaseUrlKey);
        var anonKey = await _settingsService.GetSettingAsync<string>(SupabaseAnonKeyKey);
        return (url, anonKey);
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

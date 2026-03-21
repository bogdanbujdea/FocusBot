using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Net;
using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Typed HttpClient wrapper for the FocusBot Web API.
/// Attaches the Supabase access token as a Bearer token on every request.
/// </summary>
public class FocusBotApiClient : IFocusBotApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IAuthService _authService;
    private readonly ILogger<FocusBotApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public bool IsConfigured => _authService.IsAuthenticated;

    public FocusBotApiClient(
        HttpClient httpClient,
        IAuthService authService,
        ILogger<FocusBotApiClient> logger)
    {
        _httpClient = httpClient;
        _authService = authService;
        _logger = logger;
    }

    public async Task<ApiSessionResponse?> StartSessionAsync(StartSessionPayload payload)
    {
        try
        {
            using var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, "/sessions");
            if (request is null) return null;

            request.Content = JsonContent.Create(payload, options: JsonOptions);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("StartSession failed: {StatusCode}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ApiSessionResponse>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "StartSession request failed");
            return null;
        }
    }

    public async Task<ApiSessionResponse?> EndSessionAsync(Guid sessionId, EndSessionPayload payload)
    {
        try
        {
            using var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, $"/sessions/{sessionId}/end");
            if (request is null) return null;

            request.Content = JsonContent.Create(payload, options: JsonOptions);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("EndSession failed: {StatusCode}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ApiSessionResponse>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EndSession request failed");
            return null;
        }
    }

    public async Task<ApiSessionResponse?> GetActiveSessionAsync()
    {
        try
        {
            using var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, "/sessions/active");
            if (request is null) return null;

            var response = await _httpClient.SendAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GetActiveSession failed: {StatusCode}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ApiSessionResponse>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetActiveSession request failed");
            return null;
        }
    }

    public async Task<ApiClassifyResponse?> ClassifyAsync(ClassifyPayload payload, string? byokApiKey = null)
    {
        try
        {
            using var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, "/classify");
            if (request is null) return null;

            if (!string.IsNullOrWhiteSpace(byokApiKey))
                request.Headers.Add("X-Api-Key", byokApiKey);

            request.Content = JsonContent.Create(payload, options: JsonOptions);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Classify failed: {StatusCode}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ApiClassifyResponse>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Classify request failed");
            return null;
        }
    }

    public async Task<ApiValidateKeyResponse?> ValidateKeyAsync(ValidateKeyPayload payload)
    {
        try
        {
            using var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, "/classify/validate-key");
            if (request is null) return null;

            request.Content = JsonContent.Create(payload, options: JsonOptions);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ValidateKey failed: {StatusCode}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ApiValidateKeyResponse>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ValidateKey request failed");
            return null;
        }
    }

    public async Task<ApiSubscriptionStatus?> GetSubscriptionStatusAsync()
    {
        try
        {
            using var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, "/subscriptions/status");
            if (request is null) return null;

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GetSubscriptionStatus failed: {StatusCode}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ApiSubscriptionStatus>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetSubscriptionStatus request failed");
            return null;
        }
    }

    public async Task<ApiDeviceResponse?> RegisterDeviceAsync(string name, string fingerprint)
    {
        try
        {
            using var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, "/devices");
            if (request is null) return null;

            request.Content = JsonContent.Create(new
            {
                deviceType = 1, // Desktop
                name,
                fingerprint,
                appVersion = GetAppVersion(),
                platform = "Windows"
            }, options: JsonOptions);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("RegisterDevice failed: {StatusCode}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ApiDeviceResponse>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RegisterDevice request failed");
            return null;
        }
    }

    public async Task<HttpStatusCode?> SendHeartbeatAsync(Guid deviceId)
    {
        try
        {
            var statusCode = await SendHeartbeatRequestAsync(deviceId);

            if (statusCode == HttpStatusCode.Unauthorized)
            {
                var refreshed = await _authService.RefreshTokenAsync();
                if (refreshed)
                    statusCode = await SendHeartbeatRequestAsync(deviceId);
            }

            return statusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SendHeartbeat request failed");
            return null;
        }
    }

    private async Task<HttpStatusCode> SendHeartbeatRequestAsync(Guid deviceId)
    {
        using var request = await CreateAuthorizedRequestAsync(HttpMethod.Put, $"/devices/{deviceId}/heartbeat");
        if (request is null)
            return HttpStatusCode.Unauthorized;

        request.Content = JsonContent.Create(new
        {
            appVersion = GetAppVersion(),
            platform = "Windows"
        }, options: JsonOptions);

        var response = await _httpClient.SendAsync(request);
        return response.StatusCode;
    }


    public async Task<bool> DeregisterDeviceAsync(Guid deviceId)
    {
        try
        {
            using var request = await CreateAuthorizedRequestAsync(HttpMethod.Delete, $"/devices/{deviceId}");
            if (request is null) return false;

            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DeregisterDevice request failed");
            return false;
        }
    }

    public async Task<bool> ProvisionUserAsync()
    {
        try
        {
            using var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, "/auth/me");
            if (request is null) return false;

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ProvisionUser failed: {StatusCode}", response.StatusCode);
                return false;
            }

            _logger.LogInformation("Backend user provisioned successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ProvisionUser request failed");
            return false;
        }
    }

    private async Task<HttpRequestMessage?> CreateAuthorizedRequestAsync(HttpMethod method, string path)
    {
        var token = await _authService.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("No access token available; skipping API call to {Path}", path);
            return null;
        }

        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static string GetAppVersion() =>
        System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";
}

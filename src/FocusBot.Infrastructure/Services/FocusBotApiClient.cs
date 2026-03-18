using System.Net.Http.Json;
using System.Text.Json;
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

    public async Task<ApiSessionResponse?> StartSessionAsync(string taskText, string? taskHints)
    {
        try
        {
            using var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, "/sessions");
            if (request is null) return null;

            request.Content = JsonContent.Create(new { taskText, taskHints }, options: JsonOptions);

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

            request.Content = JsonContent.Create(new
            {
                focusScorePercent = payload.FocusScorePercent,
                focusedSeconds = payload.FocusedSeconds,
                distractedSeconds = payload.DistractedSeconds,
                distractionCount = payload.DistractionCount,
                contextSwitchCostSeconds = payload.ContextSwitchCostSeconds,
                topDistractingApps = payload.TopDistractingApps
            }, options: JsonOptions);

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

    public async Task<ApiClassifyResponse?> ClassifyAsync(ClassifyPayload payload)
    {
        try
        {
            using var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, "/classify");
            if (request is null) return null;

            request.Content = JsonContent.Create(new
            {
                taskText = payload.TaskText,
                taskHints = payload.TaskHints,
                processName = payload.ProcessName,
                windowTitle = payload.WindowTitle
            }, options: JsonOptions);

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

    private async Task<HttpRequestMessage?> CreateAuthorizedRequestAsync(HttpMethod method, string path)
    {
        var token = await _authService.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("No access token available; skipping API call to {Path}", path);
            return null;
        }

        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}

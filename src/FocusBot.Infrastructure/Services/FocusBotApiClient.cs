using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Net;
using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

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

    private static readonly ResiliencePipeline<ApiResult<ApiSessionResponse>> SessionApiRetryPipeline =
        new ResiliencePipelineBuilder<ApiResult<ApiSessionResponse>>()
            .AddRetry(new RetryStrategyOptions<ApiResult<ApiSessionResponse>>
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Constant,
                ShouldHandle = static args =>
                {
                    if (args.Outcome.Exception is not null)
                        return new ValueTask<bool>(args.Outcome.Exception is HttpRequestException);
                    var r = args.Outcome.Result!;
                    return new ValueTask<bool>(ShouldRetrySessionApiResult(r));
                },
            })
            .Build();

    private static bool ShouldRetrySessionApiResult(ApiResult<ApiSessionResponse> r)
    {
        if (r.IsSuccess)
            return false;
        if (r.StatusCode is null)
            return true;
        var code = (int)r.StatusCode.Value;
        if (code == 408)
            return true;
        return code >= 500 && code <= 599;
    }

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

    public async Task<ApiResult<ApiSessionResponse>> StartSessionAsync(StartSessionPayload payload)
    {
        return await SessionApiRetryPipeline.ExecuteAsync(
            async ct => await StartSessionCoreAsync(payload, ct));
    }

    public async Task<ApiResult<ApiSessionResponse>> EndSessionAsync(Guid sessionId, EndSessionPayload payload)
    {
        return await SessionApiRetryPipeline.ExecuteAsync(
            async ct => await EndSessionCoreAsync(sessionId, payload, ct));
    }

    public async Task<ApiResult<ApiSessionResponse>> PauseSessionAsync(Guid sessionId)
    {
        return await SessionApiRetryPipeline.ExecuteAsync(
            async ct => await PauseSessionCoreAsync(sessionId, ct));
    }

    public async Task<ApiResult<ApiSessionResponse>> ResumeSessionAsync(Guid sessionId)
    {
        return await SessionApiRetryPipeline.ExecuteAsync(
            async ct => await ResumeSessionCoreAsync(sessionId, ct));
    }

    private async Task<ApiResult<ApiSessionResponse>> StartSessionCoreAsync(
        StartSessionPayload payload,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, "/sessions");
            if (request is null)
                return ApiResult<ApiSessionResponse>.NotAuthenticated();

            request.Content = JsonContent.Create(payload, options: JsonOptions);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("StartSession failed: {StatusCode}", response.StatusCode);
                return ApiResult<ApiSessionResponse>.Failure(response.StatusCode);
            }

            var body = await response.Content.ReadFromJsonAsync<ApiSessionResponse>(JsonOptions, cancellationToken);
            if (body is null)
                return ApiResult<ApiSessionResponse>.Failure(response.StatusCode);

            return ApiResult<ApiSessionResponse>.Success(body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "StartSession request failed");
            return ApiResult<ApiSessionResponse>.NetworkError();
        }
    }

    private async Task<ApiResult<ApiSessionResponse>> EndSessionCoreAsync(
        Guid sessionId,
        EndSessionPayload payload,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, $"/sessions/{sessionId}/end");
            if (request is null)
                return ApiResult<ApiSessionResponse>.NotAuthenticated();

            request.Content = JsonContent.Create(payload, options: JsonOptions);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("EndSession failed: {StatusCode}", response.StatusCode);
                return ApiResult<ApiSessionResponse>.Failure(response.StatusCode);
            }

            var body = await response.Content.ReadFromJsonAsync<ApiSessionResponse>(JsonOptions, cancellationToken);
            if (body is null)
                return ApiResult<ApiSessionResponse>.Failure(response.StatusCode);

            return ApiResult<ApiSessionResponse>.Success(body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EndSession request failed");
            return ApiResult<ApiSessionResponse>.NetworkError();
        }
    }

    private async Task<ApiResult<ApiSessionResponse>> PauseSessionCoreAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, $"/sessions/{sessionId}/pause");
            if (request is null)
                return ApiResult<ApiSessionResponse>.NotAuthenticated();

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("PauseSession failed: {StatusCode}", response.StatusCode);
                return ApiResult<ApiSessionResponse>.Failure(response.StatusCode);
            }

            var body = await response.Content.ReadFromJsonAsync<ApiSessionResponse>(JsonOptions, cancellationToken);
            if (body is null)
                return ApiResult<ApiSessionResponse>.Failure(response.StatusCode);

            return ApiResult<ApiSessionResponse>.Success(body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PauseSession request failed");
            return ApiResult<ApiSessionResponse>.NetworkError();
        }
    }

    private async Task<ApiResult<ApiSessionResponse>> ResumeSessionCoreAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, $"/sessions/{sessionId}/resume");
            if (request is null)
                return ApiResult<ApiSessionResponse>.NotAuthenticated();

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ResumeSession failed: {StatusCode}", response.StatusCode);
                return ApiResult<ApiSessionResponse>.Failure(response.StatusCode);
            }

            var body = await response.Content.ReadFromJsonAsync<ApiSessionResponse>(JsonOptions, cancellationToken);
            if (body is null)
                return ApiResult<ApiSessionResponse>.Failure(response.StatusCode);

            return ApiResult<ApiSessionResponse>.Success(body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ResumeSession request failed");
            return ApiResult<ApiSessionResponse>.NetworkError();
        }
    }

    public async Task<ApiSessionResponse?> GetActiveSessionAsync()
    {
        try
        {
            using var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, "/sessions/active");
            if (request is null) return null;

            var response = await _httpClient.SendAsync(request);

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

    public async Task<ApiClientResponse?> RegisterClientAsync(
        string name,
        string fingerprint,
        ClientType clientType = ClientType.Desktop,
        ClientHost host = ClientHost.Windows)
    {
        try
        {
            using var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, "/clients");
            if (request is null) return null;

            var payload = new RegisterClientRequest(
                clientType,
                host,
                name,
                fingerprint,
                GetAppVersion(),
                "Windows");
            request.Content = JsonContent.Create(payload, options: JsonOptions);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("RegisterClient failed: {StatusCode}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ApiClientResponse>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RegisterClient request failed");
            return null;
        }
    }

    public async Task<HttpStatusCode?> SendHeartbeatAsync(Guid clientId)
    {
        try
        {
            var statusCode = await SendHeartbeatRequestAsync(clientId);

            if (statusCode == HttpStatusCode.Unauthorized)
            {
                var refreshed = await _authService.RefreshTokenAsync();
                if (refreshed)
                    statusCode = await SendHeartbeatRequestAsync(clientId);
            }

            return statusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SendHeartbeat request failed");
            return null;
        }
    }

    private async Task<HttpStatusCode> SendHeartbeatRequestAsync(Guid clientId)
    {
        using var request = await CreateAuthorizedRequestAsync(HttpMethod.Put, $"/clients/{clientId}/heartbeat");
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


    public async Task<bool> DeregisterClientAsync(Guid clientId)
    {
        try
        {
            using var request = await CreateAuthorizedRequestAsync(HttpMethod.Delete, $"/clients/{clientId}");
            if (request is null) return false;

            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DeregisterClient request failed");
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

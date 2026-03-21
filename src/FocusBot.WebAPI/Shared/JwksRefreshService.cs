using System.Text.Json.Serialization;
using Microsoft.IdentityModel.Tokens;

namespace FocusBot.WebAPI.Shared;

/// <summary>
/// Background service that periodically fetches the Supabase JWKS endpoint
/// and exposes the signing keys for JWT Bearer validation.
/// Supabase caches the JWKS at their edge for 10 minutes; this service
/// refreshes every 5 minutes to stay within that window.
/// </summary>
public sealed class JwksRefreshService : IHostedService, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<JwksRefreshService> _logger;
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _refreshInterval = TimeSpan.FromMinutes(5);

    private IReadOnlyList<SecurityKey> _signingKeys = [];
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;
    private Task? _refreshTask;

    /// <summary>
    /// Gets the currently cached signing keys from the Supabase JWKS endpoint.
    /// </summary>
    public IReadOnlyList<SecurityKey> SigningKeys
    {
        get
        {
            lock (_lock)
            {
                return _signingKeys;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JwksRefreshService"/> class.
    /// </summary>
    public JwksRefreshService(IConfiguration configuration, ILogger<JwksRefreshService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = new HttpClient();
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await RefreshKeysAsync(cancellationToken);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _refreshTask = RunPeriodicRefreshAsync(_cts.Token);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
        }

        if (_refreshTask is not null)
        {
            try
            {
                await _refreshTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown.
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts?.Dispose();
        _httpClient.Dispose();
    }

    /// <summary>
    /// Resolves signing keys for the JWT Bearer middleware.
    /// Intended to be used as <see cref="TokenValidationParameters.IssuerSigningKeyResolver"/>.
    /// </summary>
    public IEnumerable<SecurityKey> ResolveSigningKeys(
        string token,
        SecurityToken securityToken,
        string kid,
        TokenValidationParameters validationParameters
    )
    {
        return SigningKeys;
    }

    private async Task RunPeriodicRefreshAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_refreshInterval);

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await RefreshKeysAsync(cancellationToken);
        }
    }

    private async Task RefreshKeysAsync(CancellationToken cancellationToken)
    {
        var supabaseUrl = _configuration["Supabase:Url"];
        if (string.IsNullOrWhiteSpace(supabaseUrl))
        {
            _logger.LogWarning("Supabase:Url is not configured; skipping JWKS refresh");
            return;
        }

        var jwksUrl = $"{supabaseUrl.TrimEnd('/')}/auth/v1/.well-known/jwks.json";

        try
        {
            var response = await _httpClient.GetFromJsonAsync<JwksResponse>(
                jwksUrl,
                cancellationToken
            );

            if (response?.Keys is null || response.Keys.Count == 0)
            {
                _logger.LogWarning(
                    "JWKS endpoint returned no keys from {Url}; retaining previous keys",
                    jwksUrl
                );
                return;
            }

            var keys = new List<SecurityKey>();
            foreach (var jwk in response.Keys)
            {
                keys.Add(new JsonWebKey(System.Text.Json.JsonSerializer.Serialize(jwk)));
            }

            lock (_lock)
            {
                _signingKeys = keys.AsReadOnly();
            }

            _logger.LogInformation(
                "Refreshed {Count} JWT signing key(s) from {Url}",
                keys.Count,
                jwksUrl
            );
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to refresh JWKS from {Url}; retaining previous keys",
                jwksUrl
            );
        }
    }

    /// <summary>
    /// Represents the JSON structure of a JWKS endpoint response.
    /// </summary>
    private sealed class JwksResponse
    {
        [JsonPropertyName("keys")]
        public List<System.Text.Json.JsonElement>? Keys { get; set; }
    }
}

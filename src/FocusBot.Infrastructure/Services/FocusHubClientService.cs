using FocusBot.Core.Interfaces;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// SignalR client that connects to the Focus hub on the Web API and
/// raises events when sessions are started or ended on other devices.
/// </summary>
public sealed class FocusHubClientService : IFocusHubClient, IAsyncDisposable
{
    private readonly IAuthService _authService;
    private readonly ILogger<FocusHubClientService> _logger;
    private readonly string _hubUrl;
    private HubConnection? _connection;

    public event Action<SessionStartedNotification>? SessionStarted;
    public event Action<SessionEndedNotification>? SessionEnded;
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public FocusHubClientService(
        IAuthService authService,
        ILogger<FocusHubClientService> logger,
        string? apiBaseUrl = null)
    {
        _authService = authService;
        _logger = logger;

#if DEBUG
        var baseUrl = apiBaseUrl ?? "http://localhost:5251";
#else
        var baseUrl = apiBaseUrl ?? "https://api.foqus.me";
#endif
        _hubUrl = $"{baseUrl}/hubs/focus";
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (_connection is not null)
            return;

        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options =>
            {
                options.AccessTokenProvider = async () =>
                    await _authService.GetAccessTokenAsync() ?? string.Empty;
            })
            .WithAutomaticReconnect([
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            ])
            .Build();

        _connection.On<SessionStartedNotification>("SessionStarted", n =>
        {
            _logger.LogInformation("SignalR: SessionStarted {SessionId}", n.SessionId);
            SessionStarted?.Invoke(n);
        });

        _connection.On<SessionEndedNotification>("SessionEnded", n =>
        {
            _logger.LogInformation("SignalR: SessionEnded {SessionId}", n.SessionId);
            SessionEnded?.Invoke(n);
        });

        try
        {
            await _connection.StartAsync(ct);
            _logger.LogInformation("SignalR hub connected");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to SignalR hub");
        }
    }

    public async Task DisconnectAsync()
    {
        if (_connection is null)
            return;

        try
        {
            await _connection.StopAsync();
            await _connection.DisposeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disconnecting from SignalR hub");
        }
        finally
        {
            _connection = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}

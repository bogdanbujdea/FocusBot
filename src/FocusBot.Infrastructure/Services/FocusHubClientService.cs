using FocusBot.Core.Interfaces;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// SignalR client that connects to the Focus hub on the Web API and
/// raises events when sessions change on other devices (or the same user elsewhere).
/// </summary>
public sealed class FocusHubClientService : IFocusHubClient, IAsyncDisposable
{
    private readonly IAuthService _authService;
    private readonly ILogger<FocusHubClientService> _logger;
    private readonly string _hubUrl;
    private HubConnection? _connection;

    public event Action<SessionStartedEvent>? SessionStarted;
    public event Action<SessionEndedEvent>? SessionEnded;
    public event Action<SessionPausedEvent>? SessionPaused;
    public event Action<SessionResumedEvent>? SessionResumed;
    public event Action<PlanChangedEvent>? PlanChanged;
    public event Action<ClassificationChangedEvent>? ClassificationChanged;
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public FocusHubClientService(
        IAuthService authService,
        ILogger<FocusHubClientService> logger,
        string? apiBaseUrl = null
    )
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
            .WithAutomaticReconnect(
                [
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(30),
                ]
            )
            .Build();

        _connection.On<SessionStartedEvent>("SessionStarted", n =>
        {
            _logger.LogInformation("SignalR: SessionStarted {SessionId}", n.SessionId);
            SessionStarted?.Invoke(n);
        });

        _connection.On<SessionEndedEvent>("SessionEnded", n =>
        {
            _logger.LogInformation("SignalR: SessionEnded {SessionId}", n.SessionId);
            SessionEnded?.Invoke(n);
        });

        _connection.On<SessionPausedEvent>("SessionPaused", n =>
        {
            _logger.LogInformation("SignalR: SessionPaused {SessionId}", n.SessionId);
            SessionPaused?.Invoke(n);
        });

        _connection.On<SessionResumedEvent>("SessionResumed", n =>
        {
            _logger.LogInformation("SignalR: SessionResumed {SessionId}", n.SessionId);
            SessionResumed?.Invoke(n);
        });

        _connection.On<PlanChangedEvent>("PlanChanged", _ =>
        {
            _logger.LogInformation("SignalR: PlanChanged");
            PlanChanged?.Invoke(new PlanChangedEvent());
        });

        _connection.On<ClassificationChangedEvent>("ClassificationChanged", e =>
        {
            _logger.LogInformation(
                "SignalR: ClassificationChanged source={Source} score={Score}",
                e.Source,
                e.Score);
            ClassificationChanged?.Invoke(e);
        });

        try
        {
            await _connection.StartAsync(ct);
            _logger.LogInformation("SignalR hub connected");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to SignalR hub");
            try
            {
                await _connection.DisposeAsync();
            }
            catch (Exception disposeEx)
            {
                _logger.LogTrace(disposeEx, "Disposing failed SignalR connection");
            }

            _connection = null;
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

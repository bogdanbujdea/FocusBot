using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// SignalR-backed implementation of realtime session synchronization.
/// Listens to focus hub session lifecycle events and reconciles coordinator state from the API.
/// </summary>
public sealed class SignalRSessionRealtimeAdapter : ISessionRealtimeAdapter, IAsyncDisposable
{
    private const string DesktopClientIdSettingKey = "Desktop_ClientId";

    private readonly ISessionCoordinator _coordinator;
    private readonly IAuthService _authService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<SignalRSessionRealtimeAdapter> _logger;
    private readonly string _hubUrl;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    private HubConnection? _connection;
    private Guid? _localClientId;
    private bool _disposed;

    public SignalRSessionRealtimeAdapter(
        ISessionCoordinator coordinator,
        IAuthService authService,
        ISettingsService settingsService,
        ILogger<SignalRSessionRealtimeAdapter> logger,
        string hubUrl
    )
    {
        _coordinator = coordinator;
        _authService = authService;
        _settingsService = settingsService;
        _logger = logger;
        _hubUrl = hubUrl;
    }

    public async Task ConnectAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            if (_disposed)
            {
                _logger.LogWarning("Skipping SignalR connect because adapter is disposed");
                return;
            }

            if (_connection is not null)
            {
                if (_connection.State is HubConnectionState.Connected or HubConnectionState.Connecting)
                {
                    _logger.LogDebug("SignalR already connected/connecting");
                    return;
                }

                _logger.LogInformation("Restarting existing SignalR connection");
                await _connection.DisposeAsync();
                _connection = null;
            }

            var token = await _authService.GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("Skipping SignalR connect because no access token is available");
                return;
            }

            _localClientId = await GetStoredClientIdAsync();

            var connection = new HubConnectionBuilder()
                .WithUrl(_hubUrl, options =>
                {
                    options.AccessTokenProvider = async () =>
                        await _authService.GetAccessTokenAsync() ?? string.Empty;
                })
                .WithAutomaticReconnect()
                .Build();

            connection.On<SessionStartedEvent>(
                "SessionStarted",
                async evt =>
                {
                    if (_localClientId.HasValue && evt.OriginClientId == _localClientId)
                    {
                        _logger.LogDebug(
                            "Ignoring self-origin SessionStarted event for client {ClientId}",
                            _localClientId
                        );
                        return;
                    }

                    await _coordinator.ApplyRemoteSessionStartedAsync(evt);
                }
            );

            connection.On<SessionEndedEvent>(
                "SessionEnded",
                async evt => await _coordinator.ApplyRemoteSessionEndedAsync(evt)
            );

            connection.On<SessionPausedEvent>(
                "SessionPaused",
                async evt => await _coordinator.ApplyRemoteSessionPausedAsync(evt)
            );

            connection.On<SessionResumedEvent>(
                "SessionResumed",
                async evt => await _coordinator.ApplyRemoteSessionResumedAsync(evt)
            );

            await connection.StartAsync();
            _connection = connection;
            _logger.LogInformation("SignalR session adapter connected to {HubUrl}", _hubUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect SignalR session adapter");
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            if (_connection is null)
            {
                return;
            }

            try
            {
                await _connection.StopAsync();
            }
            finally
            {
                await _connection.DisposeAsync();
                _connection = null;
            }

            _logger.LogInformation("SignalR session adapter disconnected");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disconnect SignalR session adapter");
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await DisconnectAsync();
        _connectionLock.Dispose();
        _disposed = true;
    }

    private async Task<Guid?> GetStoredClientIdAsync()
    {
        var raw = await _settingsService.GetSettingAsync<string>(DesktopClientIdSettingKey);
        if (Guid.TryParse(raw, out var parsed))
        {
            return parsed;
        }

        return null;
    }
}

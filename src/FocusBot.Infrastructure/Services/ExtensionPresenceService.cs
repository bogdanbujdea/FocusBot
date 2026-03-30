using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using FocusBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Lightweight WebSocket server that tracks browser extension presence via ping/pong.
/// Used by desktop app to determine if it should skip browser process classification.
/// </summary>
public sealed class ExtensionPresenceService : IExtensionPresenceService, IDisposable
{
    private const int PrimaryPort = 9876;
    private const int BackupPort = 9877;
    private const string Path = "/foqus-presence";

    private readonly ILogger<ExtensionPresenceService> _logger;
    private readonly object _lock = new();
    private HttpListener? _listener;
    private WebSocket? _activeConnection;
    private CancellationTokenSource? _listenerCts;
    private bool _isRunning;
    private bool _wasOnline;

    public bool IsExtensionOnline
    {
        get
        {
            lock (_lock)
            {
                return _activeConnection?.State == WebSocketState.Open;
            }
        }
    }

    public event EventHandler? ConnectionStateChanged;

    public ExtensionPresenceService(ILogger<ExtensionPresenceService> logger)
    {
        _logger = logger;
    }

    public async Task StartAsync()
    {
        lock (_lock)
        {
            if (_isRunning)
                return;
            _isRunning = true;
        }

        _listenerCts = new CancellationTokenSource();

        if (!await TryStartListenerAsync(PrimaryPort, _listenerCts.Token))
        {
            _logger.LogWarning(
                "Primary port {Port} unavailable, trying backup port {BackupPort}",
                PrimaryPort,
                BackupPort
            );
            if (!await TryStartListenerAsync(BackupPort, _listenerCts.Token))
            {
                _logger.LogError(
                    "Failed to start extension presence server on both ports {Primary} and {Backup}",
                    PrimaryPort,
                    BackupPort
                );
                lock (_lock)
                {
                    _isRunning = false;
                }
                return;
            }
        }

        _ = AcceptConnectionsAsync(_listenerCts.Token);
    }

    private Task<bool> TryStartListenerAsync(int port, CancellationToken ct)
    {
        try
        {
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}{Path}/");
            listener.Start();

            _listener = listener;
            _logger.LogInformation("Extension presence server started on port {Port}", port);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start listener on port {Port}", port);
            return Task.FromResult(false);
        }
    }

    private async Task AcceptConnectionsAsync(CancellationToken ct)
    {
        if (_listener is null)
            return;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync().WaitAsync(ct);
                if (!context.Request.IsWebSocketRequest)
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                    continue;
                }

                var wsContext = await context.AcceptWebSocketAsync(null);
                _logger.LogInformation("Extension connected");

                bool stateChanged;
                lock (_lock)
                {
                    _activeConnection?.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "New connection",
                        CancellationToken.None
                    );
                    _activeConnection = wsContext.WebSocket;
                    stateChanged = !_wasOnline;
                    _wasOnline = true;
                }

                if (stateChanged)
                    ConnectionStateChanged?.Invoke(this, EventArgs.Empty);

                _ = HandleConnectionAsync(wsContext.WebSocket, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error accepting WebSocket connection");
            }
        }
    }

    private async Task HandleConnectionAsync(WebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[1024];

        try
        {
            while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Client closed",
                        ct
                    );
                    _logger.LogInformation("Extension disconnected");
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var message = JsonSerializer.Deserialize<PresenceMessage>(json);

                    if (message?.Type == "ping")
                    {
                        var pong = JsonSerializer.Serialize(new PresenceMessage { Type = "pong" });
                        var pongBytes = Encoding.UTF8.GetBytes(pong);
                        await socket.SendAsync(
                            new ArraySegment<byte>(pongBytes),
                            WebSocketMessageType.Text,
                            true,
                            ct
                        );
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WebSocket connection error");
        }
        finally
        {
            bool stateChanged;
            lock (_lock)
            {
                if (_activeConnection == socket)
                {
                    _activeConnection = null;
                }
                stateChanged = _wasOnline;
                _wasOnline = false;
            }

            if (stateChanged)
                ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (!_isRunning)
                return;
            _isRunning = false;
        }

        _listenerCts?.Cancel();
        _activeConnection?.CloseAsync(
            WebSocketCloseStatus.NormalClosure,
            "Server stopping",
            CancellationToken.None
        );
        _listener?.Stop();
        _logger.LogInformation("Extension presence server stopped");
    }

    public void Dispose()
    {
        Stop();
        _listenerCts?.Dispose();
        _activeConnection?.Dispose();
        _listener?.Close();
    }

    private sealed class PresenceMessage
    {
        public string Type { get; set; } = string.Empty;
    }
}

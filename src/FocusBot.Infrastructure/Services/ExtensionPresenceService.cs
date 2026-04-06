using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using FocusBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// WebSocket server that accepts connections from the browser extension for presence signaling.
/// When the extension is connected, the desktop app skips classifying Chromium browser windows.
/// </summary>
public sealed class ExtensionPresenceService : IExtensionPresenceService, IDisposable
{
    private const int PrimaryPort = 9876;
    private const int BackupPort = 9877;
    private const string Path = "/foqus-presence/";
    private const int PresenceTimeoutSeconds = 60;
    private const int ReceiveBufferSize = 1024;

    private readonly ILogger<ExtensionPresenceService> _logger;
    private readonly object _lock = new();

    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private Task? _timeoutTask;
    private DateTime _lastPingUtc = DateTime.MinValue;
    private bool _isOnline;
    private int _activePort;

    public bool IsExtensionOnline
    {
        get
        {
            lock (_lock)
            {
                return _isOnline;
            }
        }
    }

    public event Action? ExtensionConnected;
    public event Action? ExtensionDisconnected;

    public ExtensionPresenceService(ILogger<ExtensionPresenceService> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_listener != null)
        {
            _logger.LogDebug("ExtensionPresenceService already started");
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (!TryStartListener(PrimaryPort) && !TryStartListener(BackupPort))
        {
            _logger.LogWarning(
                "Failed to start WebSocket server on ports {Primary} and {Backup}",
                PrimaryPort,
                BackupPort);
            return Task.CompletedTask;
        }

        _logger.LogInformation(
            "Extension presence WebSocket server started on port {Port}",
            _activePort);

        _listenTask = AcceptConnectionsAsync(_cts.Token);
        _timeoutTask = MonitorTimeoutAsync(_cts.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_cts != null)
        {
            await _cts.CancelAsync();
        }

        _listener?.Stop();
        _listener?.Close();
        _listener = null;

        if (_listenTask != null)
        {
            try
            {
                await _listenTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        if (_timeoutTask != null)
        {
            try
            {
                await _timeoutTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _cts?.Dispose();
        _cts = null;
        _listenTask = null;
        _timeoutTask = null;

        SetOffline();
        _logger.LogInformation("Extension presence WebSocket server stopped");
    }

    public void Dispose()
    {
        _ = StopAsync();
    }

    private bool TryStartListener(int port)
    {
        try
        {
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}{Path}");
            listener.Start();
            _listener = listener;
            _activePort = port;
            return true;
        }
        catch (HttpListenerException ex)
        {
            _logger.LogDebug(
                ex,
                "Could not bind to port {Port}",
                port);
            return false;
        }
    }

    private async Task AcceptConnectionsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null)
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

                _ = HandleWebSocketAsync(context, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 995)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting WebSocket connection");
            }
        }
    }

    private async Task HandleWebSocketAsync(HttpListenerContext context, CancellationToken ct)
    {
        WebSocket? webSocket = null;
        try
        {
            var wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
            webSocket = wsContext.WebSocket;

            _logger.LogInformation("Browser extension connected");
            SetOnline();

            var buffer = new byte[ReceiveBufferSize];

            while (webSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("Browser extension disconnected");
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await HandleMessageAsync(webSocket, message, ct);
                }
            }
        }
        catch (WebSocketException ex)
        {
            _logger.LogDebug(ex, "WebSocket error");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling WebSocket connection");
        }
        finally
        {
            if (webSocket != null)
            {
                try
                {
                    if (webSocket.State == WebSocketState.Open)
                    {
                        await webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Server closing",
                            CancellationToken.None);
                    }
                }
                catch
                {
                }

                webSocket.Dispose();
            }
        }
    }

    private async Task HandleMessageAsync(WebSocket webSocket, string message, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(message);
            var type = doc.RootElement.GetProperty("type").GetString();

            if (string.Equals(type, "ping", StringComparison.OrdinalIgnoreCase))
            {
                lock (_lock)
                {
                    _lastPingUtc = DateTime.UtcNow;
                }

                SetOnline();

                var pong = JsonSerializer.Serialize(new { type = "pong" });
                var pongBytes = Encoding.UTF8.GetBytes(pong);
                await webSocket.SendAsync(
                    new ArraySegment<byte>(pongBytes),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    ct);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Invalid JSON message: {Message}", message);
        }
    }

    private async Task MonitorTimeoutAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct);

                bool shouldGoOffline;
                lock (_lock)
                {
                    var elapsed = DateTime.UtcNow - _lastPingUtc;
                    shouldGoOffline = _isOnline && elapsed.TotalSeconds > PresenceTimeoutSeconds;
                }

                if (shouldGoOffline)
                {
                    _logger.LogInformation(
                        "Extension presence timeout ({Timeout}s without ping)",
                        PresenceTimeoutSeconds);
                    SetOffline();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void SetOnline()
    {
        bool wasOffline;
        lock (_lock)
        {
            wasOffline = !_isOnline;
            _isOnline = true;
        }

        if (wasOffline)
        {
            _logger.LogDebug("Extension is now online");
            ExtensionConnected?.Invoke();
        }
    }

    private void SetOffline()
    {
        bool wasOnline;
        lock (_lock)
        {
            wasOnline = _isOnline;
            _isOnline = false;
        }

        if (wasOnline)
        {
            _logger.LogDebug("Extension is now offline");
            ExtensionDisconnected?.Invoke();
        }
    }
}

using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using FocusBot.Core.DTOs;
using FocusBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FocusBot.Infrastructure.Services;

public class WebSocketIntegrationService : IIntegrationService
{
    private readonly ILogger<WebSocketIntegrationService> _logger;
    private HttpListener? _httpListener;
    private CancellationTokenSource? _cts;
    private WebSocket? _clientSocket;
    private Task? _acceptTask;
    private Task? _receiveTask;
    private bool _disposed;
    private BrowserContextPayload? _lastBrowserContext;

    private const int Port = 9876;
    private const string Path = "/focusbot";
    private const int ReceiveBufferSize = 8192;

    public bool IsExtensionConnected => _clientSocket?.State == WebSocketState.Open;
    public BrowserContextPayload? LastBrowserContext => _lastBrowserContext;

    public event EventHandler<bool>? ExtensionConnectionChanged;
    public event EventHandler<TaskStartedPayload>? TaskStartedReceived;
    public event EventHandler? TaskEndedReceived;
    public event EventHandler<FocusStatusPayload>? FocusStatusReceived;
    public event EventHandler<DesktopForegroundPayload>? DesktopForegroundReceived;
    public event EventHandler<BrowserContextPayload>? BrowserContextReceived;

    public WebSocketIntegrationService(ILogger<WebSocketIntegrationService> logger)
    {
        _logger = logger;
    }

    public Task StartAsync()
    {
        if (_httpListener != null)
            return Task.CompletedTask;

        _cts = new CancellationTokenSource();

        try
        {
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://localhost:{Port}{Path}/");
            _httpListener.Start();
            _logger.LogInformation("WebSocket server started on ws://localhost:{Port}{Path}", Port, Path);
            _acceptTask = AcceptConnectionsAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start WebSocket server on port {Port}", Port);
            _httpListener?.Close();
            _httpListener = null;
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();

        if (_clientSocket?.State == WebSocketState.Open)
        {
            try
            {
                await _clientSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Server shutting down",
                    CancellationToken.None
                ).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error closing WebSocket client connection");
            }
        }
        _clientSocket?.Dispose();
        _clientSocket = null;

        _httpListener?.Stop();
        _httpListener?.Close();
        _httpListener = null;

        if (_acceptTask != null)
        {
            try { await _acceptTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { _logger.LogDebug(ex, "Accept task completed with error"); }
        }

        if (_receiveTask != null)
        {
            try { await _receiveTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { _logger.LogDebug(ex, "Receive task completed with error"); }
        }

        _logger.LogInformation("WebSocket server stopped");
    }

    private async Task AcceptConnectionsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _httpListener != null)
        {
            try
            {
                var context = await _httpListener.GetContextAsync().ConfigureAwait(false);

                if (!context.Request.IsWebSocketRequest)
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                    continue;
                }

                if (_clientSocket?.State == WebSocketState.Open)
                {
                    try
                    {
                        await _clientSocket.CloseAsync(
                            WebSocketCloseStatus.PolicyViolation,
                            "New client connecting",
                            CancellationToken.None
                        ).ConfigureAwait(false);
                    }
                    catch { }
                    _clientSocket.Dispose();
                }

                var wsContext = await context.AcceptWebSocketAsync(null).ConfigureAwait(false);
                _clientSocket = wsContext.WebSocket;
                _logger.LogInformation("Extension connected");
                ExtensionConnectionChanged?.Invoke(this, true);

                _receiveTask = ReceiveLoopAsync(_clientSocket, ct);
            }
            catch (ObjectDisposedException) { break; }
            catch (HttpListenerException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting WebSocket connection");
                await Task.Delay(1000, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task ReceiveLoopAsync(WebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[ReceiveBufferSize];
        var messageBuffer = new StringBuilder();

        try
        {
            while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    ct
                ).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("Extension disconnected");
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    messageBuffer.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                    if (result.EndOfMessage)
                    {
                        var json = messageBuffer.ToString();
                        messageBuffer.Clear();
                        HandleMessage(json);
                    }
                }
            }
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            _logger.LogInformation("Extension connection closed prematurely");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in WebSocket receive loop");
        }
        finally
        {
            ExtensionConnectionChanged?.Invoke(this, false);
        }
    }

    private void HandleMessage(string json)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<IntegrationEnvelope>(json);
            if (envelope == null)
                return;

            _logger.LogDebug("Received message: {Type}", envelope.Type);

            switch (envelope.Type)
            {
                case IntegrationMessageTypes.Handshake:
                    HandleHandshake(envelope);
                    break;

                case IntegrationMessageTypes.TaskStarted:
                    HandleTaskStarted(envelope);
                    break;

                case IntegrationMessageTypes.TaskEnded:
                    HandleTaskEnded(envelope);
                    break;

                case IntegrationMessageTypes.FocusStatus:
                    HandleFocusStatus(envelope);
                    break;

                case IntegrationMessageTypes.DesktopForeground:
                    HandleDesktopForeground(envelope);
                    break;

                case IntegrationMessageTypes.BrowserContext:
                    HandleBrowserContext(envelope);
                    break;

                default:
                    _logger.LogWarning("Unknown message type: {Type}", envelope.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling WebSocket message");
        }
    }

    private void HandleHandshake(IntegrationEnvelope envelope)
    {
        if (envelope.Payload == null)
            return;

        var payload = JsonSerializer.Deserialize<HandshakePayload>(envelope.Payload.Value.GetRawText());
        if (payload == null)
            return;

        _logger.LogInformation("Handshake from {Source}, hasActiveTask={HasActive}", payload.Source, payload.HasActiveTask);

        if (payload.HasActiveTask && !string.IsNullOrEmpty(payload.SessionTitle))
        {
            TaskStartedReceived?.Invoke(this, new TaskStartedPayload
            {
                TaskId = payload.TaskId ?? string.Empty,
                SessionTitle = payload.SessionTitle,
                SessionContext = payload.SessionContext,
                StartedAt = payload.StartedAt
            });
        }
    }

    private void HandleTaskStarted(IntegrationEnvelope envelope)
    {
        if (envelope.Payload == null)
            return;

        var payload = JsonSerializer.Deserialize<TaskStartedPayload>(envelope.Payload.Value.GetRawText());
        if (payload == null)
            return;

        TaskStartedReceived?.Invoke(this, payload);
    }

    private void HandleTaskEnded(IntegrationEnvelope envelope)
    {
        TaskEndedReceived?.Invoke(this, EventArgs.Empty);
    }

    private void HandleFocusStatus(IntegrationEnvelope envelope)
    {
        if (envelope.Payload == null)
            return;

        var payload = JsonSerializer.Deserialize<FocusStatusPayload>(envelope.Payload.Value.GetRawText());
        if (payload != null)
            FocusStatusReceived?.Invoke(this, payload);
    }

    private void HandleDesktopForeground(IntegrationEnvelope envelope)
    {
        if (envelope.Payload == null)
            return;

        var payload = JsonSerializer.Deserialize<DesktopForegroundPayload>(envelope.Payload.Value.GetRawText());
        if (payload != null)
            DesktopForegroundReceived?.Invoke(this, payload);
    }

    private void HandleBrowserContext(IntegrationEnvelope envelope)
    {
        if (envelope.Payload == null)
            return;

        var payload = JsonSerializer.Deserialize<BrowserContextPayload>(envelope.Payload.Value.GetRawText());
        if (payload == null)
            return;

        _lastBrowserContext = payload;
        BrowserContextReceived?.Invoke(this, payload);
    }

    private async Task SendMessageAsync(string type, object? payload = null)
    {
        if (_clientSocket?.State != WebSocketState.Open)
            return;

        try
        {
            var envelope = new IntegrationEnvelope
            {
                Type = type,
                Payload = payload != null
                    ? JsonSerializer.SerializeToElement(payload)
                    : null
            };

            var json = JsonSerializer.Serialize(envelope);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _clientSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                _cts?.Token ?? CancellationToken.None
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending WebSocket message of type {Type}", type);
        }
    }

    public async Task SendHandshakeAsync(bool hasActiveTask, string? taskId, string? sessionTitle, string? sessionContext)
    {
        await SendMessageAsync(IntegrationMessageTypes.Handshake, new HandshakePayload
        {
            Source = "app",
            HasActiveTask = hasActiveTask,
            TaskId = taskId,
            SessionTitle = sessionTitle,
            SessionContext = sessionContext
        }).ConfigureAwait(false);
    }

    public async Task SendTaskStartedAsync(string taskId, string sessionTitle, string? sessionContext)
    {
        await SendMessageAsync(IntegrationMessageTypes.TaskStarted, new TaskStartedPayload
        {
            TaskId = taskId,
            SessionTitle = sessionTitle,
            SessionContext = sessionContext
        }).ConfigureAwait(false);
    }

    public async Task SendTaskEndedAsync(string taskId)
    {
        await SendMessageAsync(IntegrationMessageTypes.TaskEnded, new TaskEndedPayload
        {
            TaskId = taskId
        }).ConfigureAwait(false);
    }

    public async Task SendFocusStatusAsync(FocusStatusPayload payload)
    {
        await SendMessageAsync(IntegrationMessageTypes.FocusStatus, payload).ConfigureAwait(false);
    }

    public async Task SendDesktopForegroundAsync(string processName, string windowTitle)
    {
        await SendMessageAsync(IntegrationMessageTypes.DesktopForeground, new DesktopForegroundPayload
        {
            ProcessName = processName,
            WindowTitle = windowTitle
        }).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _ = StopAsync();
        _cts?.Dispose();
        _clientSocket?.Dispose();
        GC.SuppressFinalize(this);
    }
}

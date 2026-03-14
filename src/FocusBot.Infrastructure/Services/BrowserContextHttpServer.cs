using System.Net;
using System.Text;
using System.Text.Json;
using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Local HTTP server that receives browser activity events from the Chrome extension
/// and serves the current focus state for the extension's overlay.
/// </summary>
public sealed class BrowserContextHttpServer : IBrowserContextService, IDisposable
{
    private readonly ILogger<BrowserContextHttpServer> _logger;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private BrowserActivityEvent? _latestContext;
    private FocusStateResponse _currentFocusState = new();
    private readonly object _lock = new();

    public const int Port = 51789;
    private static readonly string Prefix = $"http://localhost:{Port}/";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public event EventHandler<BrowserActivityEvent>? BrowserActivityReceived;

    public BrowserContextHttpServer(ILogger<BrowserContextHttpServer> logger)
    {
        _logger = logger;
    }

    public BrowserActivityEvent? GetLatestContext()
    {
        lock (_lock)
            return _latestContext;
    }

    public void UpdateFocusState(string status, string? taskName, string? reason, long sessionElapsedSeconds)
    {
        lock (_lock)
        {
            _currentFocusState = new FocusStateResponse
            {
                Status = status,
                TaskName = taskName,
                Reason = reason,
                SessionElapsedSeconds = sessionElapsedSeconds,
                Connected = true
            };
        }
    }

    public FocusStateResponse GetCurrentFocusState()
    {
        lock (_lock)
            return _currentFocusState;
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        try
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _listener = new HttpListener();
            _listener.Prefixes.Add(Prefix);
            _listener.Start();
            _listenTask = Task.Run(() => ListenLoop(_cts.Token), _cts.Token);
            _logger.LogInformation("Browser context HTTP server started on {Prefix}", Prefix);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start browser context HTTP server on {Prefix}", Prefix);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        _listener?.Stop();

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

        _listener?.Close();
        _logger.LogInformation("Browser context HTTP server stopped");
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener is { IsListening: true })
        {
            try
            {
                var context = await _listener.GetContextAsync().WaitAsync(ct);
                _ = Task.Run(() => HandleRequest(context), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error accepting HTTP request");
            }
        }
    }

    private async Task HandleRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        AddCorsHeaders(response);

        try
        {
            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 204;
                response.Close();
                return;
            }

            var path = request.Url?.AbsolutePath ?? string.Empty;

            if (path == "/api/browser-activity" && request.HttpMethod == "POST")
                await HandleBrowserActivity(request, response);
            else if (path == "/api/focus-state" && request.HttpMethod == "GET")
                await HandleFocusState(response);
            else
            {
                response.StatusCode = 404;
                response.Close();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error handling HTTP request");
            response.StatusCode = 500;
            response.Close();
        }
    }

    private async Task HandleBrowserActivity(HttpListenerRequest request, HttpListenerResponse response)
    {
        using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();

        var activityEvent = JsonSerializer.Deserialize<BrowserActivityEvent>(body, JsonOptions);
        if (activityEvent == null)
        {
            response.StatusCode = 400;
            response.Close();
            return;
        }

        lock (_lock)
            _latestContext = activityEvent;

        BrowserActivityReceived?.Invoke(this, activityEvent);

        response.StatusCode = 200;
        response.ContentType = "application/json";
        var responseBytes = Encoding.UTF8.GetBytes("{\"ok\":true}");
        await response.OutputStream.WriteAsync(responseBytes);
        response.Close();
    }

    private async Task HandleFocusState(HttpListenerResponse response)
    {
        var state = GetCurrentFocusState();
        var json = JsonSerializer.Serialize(state, JsonOptions);

        response.StatusCode = 200;
        response.ContentType = "application/json";
        var responseBytes = Encoding.UTF8.GetBytes(json);
        await response.OutputStream.WriteAsync(responseBytes);
        response.Close();
    }

    private static void AddCorsHeaders(HttpListenerResponse response)
    {
        response.AddHeader("Access-Control-Allow-Origin", "*");
        response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        response.AddHeader("Access-Control-Allow-Headers", "Content-Type");
        response.AddHeader("Access-Control-Max-Age", "86400");
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _listener?.Close();
        _cts?.Dispose();
    }
}

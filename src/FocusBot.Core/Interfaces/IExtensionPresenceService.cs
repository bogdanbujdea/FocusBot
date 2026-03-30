namespace FocusBot.Core.Interfaces;

/// <summary>
/// Tracks browser extension presence via local WebSocket.
/// </summary>
public interface IExtensionPresenceService
{
    /// <summary>
    /// True when extension is connected and sending heartbeats (ping received in last 60s).
    /// </summary>
    bool IsExtensionOnline { get; }

    /// <summary>
    /// Raised when extension connection state changes (connected or disconnected).
    /// </summary>
    event EventHandler? ConnectionStateChanged;

    /// <summary>
    /// Starts the WebSocket presence server.
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// Stops the WebSocket presence server.
    /// </summary>
    void Stop();
}

namespace FocusBot.Core.Interfaces;

/// <summary>
/// Manages local WebSocket presence signaling with the browser extension.
/// When the extension is online, the desktop app skips classifying Chromium browser
/// windows to avoid duplicate API calls (the extension has richer context).
/// </summary>
public interface IExtensionPresenceService
{
    /// <summary>
    /// Returns true if the browser extension has sent a ping within the timeout window.
    /// </summary>
    bool IsExtensionOnline { get; }

    /// <summary>
    /// Raised when the extension connects or sends its first ping after being offline.
    /// </summary>
    event Action? ExtensionConnected;

    /// <summary>
    /// Raised when the extension disconnects or stops sending pings (timeout).
    /// </summary>
    event Action? ExtensionDisconnected;

    /// <summary>
    /// Starts the WebSocket server listening for extension connections.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the WebSocket server and closes any active connections.
    /// </summary>
    Task StopAsync();
}

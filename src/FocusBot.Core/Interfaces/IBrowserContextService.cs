using FocusBot.Core.Entities;

namespace FocusBot.Core.Interfaces;

/// <summary>
/// Service that receives browser context from the Chrome extension companion and exposes
/// current focus state for the extension's in-browser overlay.
/// </summary>
public interface IBrowserContextService
{
    /// <summary>
    /// Returns the most recent browser activity event, or null if none has been received.
    /// </summary>
    BrowserActivityEvent? GetLatestContext();

    /// <summary>
    /// Updates the current focus state that the extension polls for overlay display.
    /// </summary>
    void UpdateFocusState(string status, string? taskName, string? reason, long sessionElapsedSeconds);

    /// <summary>
    /// Returns the current focus state for the Chrome extension overlay.
    /// </summary>
    FocusStateResponse GetCurrentFocusState();

    /// <summary>
    /// Raised when a new browser activity event is received from the extension.
    /// </summary>
    event EventHandler<BrowserActivityEvent>? BrowserActivityReceived;

    /// <summary>
    /// Starts the local HTTP server that listens for extension requests.
    /// </summary>
    Task StartAsync(CancellationToken ct = default);

    /// <summary>
    /// Stops the local HTTP server.
    /// </summary>
    Task StopAsync();
}

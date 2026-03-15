using FocusBot.Core.DTOs;
using FocusBot.Core.Entities;

namespace FocusBot.Core.Interfaces;

/// <summary>
/// Manages bidirectional communication between the Windows app and browser extension.
/// </summary>
public interface IIntegrationService : IDisposable
{
    IntegrationMode CurrentMode { get; }
    bool IsExtensionConnected { get; }

    Task StartAsync();
    Task StopAsync();

    Task SendHandshakeAsync(bool hasActiveTask, string? taskId, string? taskText, string? taskHints);
    Task SendTaskStartedAsync(string taskId, string taskText, string? taskHints);
    Task SendTaskEndedAsync(string taskId);
    Task SendFocusStatusAsync(FocusStatusPayload payload);
    Task SendDesktopForegroundAsync(string processName, string windowTitle);
    Task<BrowserUrlResponsePayload?> RequestBrowserUrlAsync(TimeSpan timeout);

    event EventHandler<bool>? ExtensionConnectionChanged;
    event EventHandler<IntegrationMode>? ModeChanged;
    event EventHandler<TaskStartedPayload>? TaskStartedReceived;
    event EventHandler? TaskEndedReceived;
    event EventHandler<FocusStatusPayload>? FocusStatusReceived;
    event EventHandler<DesktopForegroundPayload>? DesktopForegroundReceived;
    event EventHandler<BrowserUrlResponsePayload>? BrowserUrlResponseReceived;
}

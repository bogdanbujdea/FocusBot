using FocusBot.Core.DTOs;

namespace FocusBot.Core.Interfaces;

/// <summary>
/// Manages bidirectional communication between the Windows app and browser extension.
/// </summary>
public interface IIntegrationService : IDisposable
{
    bool IsExtensionConnected { get; }

    Task StartAsync();
    Task StopAsync();

    Task SendHandshakeAsync(bool hasActiveTask, string? taskId, string? sessionTitle, string? sessionContext);
    Task SendTaskStartedAsync(string taskId, string sessionTitle, string? sessionContext);
    Task SendTaskEndedAsync(string taskId);
    Task SendFocusStatusAsync(FocusStatusPayload payload);
    Task SendDesktopForegroundAsync(string processName, string windowTitle);

    BrowserContextPayload? LastBrowserContext { get; }

    event EventHandler<bool>? ExtensionConnectionChanged;
    event EventHandler<TaskStartedPayload>? TaskStartedReceived;
    event EventHandler? TaskEndedReceived;
    event EventHandler<FocusStatusPayload>? FocusStatusReceived;
    event EventHandler<DesktopForegroundPayload>? DesktopForegroundReceived;
    event EventHandler<BrowserContextPayload>? BrowserContextReceived;
}

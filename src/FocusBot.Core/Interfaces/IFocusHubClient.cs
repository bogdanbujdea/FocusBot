namespace FocusBot.Core.Interfaces;

/// <summary>
/// Client-side interface for receiving real-time session events from the server
/// via SignalR. Implementations handle incoming notifications from other devices.
/// </summary>
public interface IFocusHubClient
{
    event Action<SessionStartedNotification>? SessionStarted;
    event Action<SessionEndedNotification>? SessionEnded;

    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync();
    bool IsConnected { get; }
}

public sealed record SessionStartedNotification(
    Guid SessionId,
    string SessionTitle,
    string? SessionContext,
    DateTime StartedAtUtc,
    string Source
);

public sealed record SessionEndedNotification(
    Guid SessionId,
    DateTime EndedAtUtc,
    string Source
);

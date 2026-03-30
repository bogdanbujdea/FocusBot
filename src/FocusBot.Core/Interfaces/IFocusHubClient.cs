namespace FocusBot.Core.Interfaces;

/// <summary>
/// Client-side interface for receiving real-time session events from the server
/// via SignalR. Implementations handle incoming notifications from other devices.
/// Payload shapes match <c>FocusBot.WebAPI.Hubs</c> hub events for correct deserialization.
/// </summary>
public interface IFocusHubClient
{
    event Action<SessionStartedEvent>? SessionStarted;
    event Action<SessionEndedEvent>? SessionEnded;
    event Action<SessionPausedEvent>? SessionPaused;
    event Action<SessionResumedEvent>? SessionResumed;
    event Action<PlanChangedEvent>? PlanChanged;
    event Action<ClassificationChangedEvent>? ClassificationChanged;

    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync();
    bool IsConnected { get; }
}

public sealed record SessionStartedEvent(
    Guid SessionId,
    string SessionTitle,
    string? SessionContext,
    DateTime StartedAtUtc,
    string Source
);

public sealed record SessionEndedEvent(Guid SessionId, DateTime EndedAtUtc, string Source);

public sealed record SessionPausedEvent(Guid SessionId, DateTime PausedAtUtc, string Source);

public sealed record SessionResumedEvent(Guid SessionId, string Source);

public sealed record PlanChangedEvent();

/// <summary>
/// Payload for hub event ClassificationChanged; mirrors WebAPI FocusHub classification broadcast.
/// </summary>
public sealed record ClassificationChangedEvent(
    int Score,
    string Reason,
    string Source,
    string ActivityName,
    DateTime ClassifiedAtUtc,
    bool Cached
);

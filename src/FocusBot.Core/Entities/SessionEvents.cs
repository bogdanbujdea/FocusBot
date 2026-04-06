namespace FocusBot.Core.Entities;

/// <summary>
/// Lightweight event emitted by the Focus hub when a session is started.
/// </summary>
public sealed record SessionStartedEvent(
    Guid SessionId,
    string SessionTitle,
    string? SessionContext,
    DateTime StartedAtUtc,
    string Source,
    Guid? OriginClientId = null
);

public sealed record SessionEndedEvent(Guid SessionId, DateTime EndedAtUtc, string Source);

public sealed record SessionPausedEvent(Guid SessionId, DateTime PausedAtUtc, string Source);

public sealed record SessionResumedEvent(Guid SessionId, string Source);

/// <summary>
/// Raised by the hub after a successful classify call.
/// Source is "extension" or "desktop"; ActivityName is the URL or window context.
/// </summary>
public sealed record ClassificationChangedEvent(
    int Score,
    string Reason,
    string Source,
    string ActivityName,
    DateTime ClassifiedAtUtc,
    bool Cached
);

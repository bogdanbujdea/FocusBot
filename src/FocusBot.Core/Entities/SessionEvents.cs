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

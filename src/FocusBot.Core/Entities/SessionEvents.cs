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

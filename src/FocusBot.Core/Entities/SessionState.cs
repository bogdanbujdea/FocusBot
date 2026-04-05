namespace FocusBot.Core.Entities;

/// <summary>
/// Immutable snapshot of the current session lifecycle state.
/// Emitted by ISessionCoordinator on every state transition.
/// </summary>
public sealed record SessionState(
    ApiSessionResponse? ActiveSession,
    string? ErrorMessage,
    SessionChangeType LastChangeType
)
{
    public bool HasActiveSession => ActiveSession is not null;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public static SessionState Initial() => new(
        ActiveSession: null,
        ErrorMessage: null,
        LastChangeType: SessionChangeType.Started
    );
}

/// <summary>
/// Describes the type of state change that occurred.
/// Used for event metadata and consumer filtering.
/// </summary>
public enum SessionChangeType
{
    Started,
    Paused,
    Resumed,
    Stopped,
    Failed,
    Synced
}

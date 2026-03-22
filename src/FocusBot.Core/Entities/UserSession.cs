namespace FocusBot.Core.Entities;

/// <summary>
/// In-memory view of an active focus session (backed by the Web API only).
/// </summary>
public sealed class UserSession
{
    /// <summary>Server session id (API session <c>Id</c> as a string).</summary>
    public string SessionId { get; init; } = string.Empty;

    public string SessionTitle { get; init; } = string.Empty;
    public string? Context { get; init; }
    public bool IsCompleted { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public long TotalElapsedSeconds { get; init; }
    public int? FocusScorePercent { get; init; }

    public long FocusedSeconds { get; init; }
    public long DistractedSeconds { get; init; }
    public int DistractionCount { get; init; }
    public int ContextSwitchCount { get; init; }
    public string? TopDistractingApps { get; init; }
    public string? TopAlignedApps { get; init; }

    public bool IsActive => !IsCompleted;

    /// <summary>
    /// Builds a session DTO from the active-session API model for UI and orchestration.
    /// </summary>
    public static UserSession FromApiResponse(ApiSessionResponse response)
    {
        var elapsed = (long)Math.Max(0, (DateTime.UtcNow - response.StartedAtUtc).TotalSeconds);
        return new UserSession
        {
            SessionId = response.Id.ToString(),
            SessionTitle = response.SessionTitle,
            Context = response.SessionContext,
            IsCompleted = false,
            CreatedAt = response.StartedAtUtc,
            TotalElapsedSeconds = elapsed,
        };
    }
}

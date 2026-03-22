namespace FocusBot.WebAPI.Features.Sessions;

/// <summary>Request body for starting a new focus session.</summary>
public sealed record StartSessionRequest(string SessionTitle, string? SessionContext, Guid? DeviceId);

/// <summary>Request body for ending an active focus session with summary metrics.</summary>
public sealed record EndSessionRequest(
    int FocusScorePercent,
    long FocusedSeconds,
    long DistractedSeconds,
    int DistractionCount,
    int ContextSwitchCount,
    Guid? DeviceId
);

/// <summary>Response DTO for a single focus session.</summary>
public sealed record SessionResponse(
    Guid Id,
    string SessionTitle,
    string? SessionContext,
    Guid? DeviceId,
    DateTime StartedAtUtc,
    DateTime? EndedAtUtc,
    DateTime? PausedAtUtc,
    long TotalPausedSeconds,
    bool IsPaused,
    int? FocusScorePercent,
    long? FocusedSeconds,
    long? DistractedSeconds,
    int? DistractionCount,
    int? ContextSwitchCount,
    string Source
);

/// <summary>Filter and sort parameters for session queries.</summary>
public sealed record SessionFilter(
    Guid? DeviceId,
    DateTime? From,
    DateTime? To,
    string? SessionTitle,
    string SortBy = "startedAt",
    string SortOrder = "desc"
);

/// <summary>Generic paginated response wrapper.</summary>
public sealed record PaginatedResponse<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize
);

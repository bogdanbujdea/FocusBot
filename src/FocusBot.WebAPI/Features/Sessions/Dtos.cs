namespace FocusBot.WebAPI.Features.Sessions;

/// <summary>Request body for starting a new focus session.</summary>
public sealed record StartSessionRequest(string TaskText, string? TaskHints);

/// <summary>Request body for ending an active focus session with summary metrics.</summary>
public sealed record EndSessionRequest(
    int FocusScorePercent,
    long FocusedSeconds,
    long DistractedSeconds,
    int DistractionCount,
    int ContextSwitchCostSeconds,
    string? TopDistractingApps);

/// <summary>Response DTO for a single focus session.</summary>
public sealed record SessionResponse(
    Guid Id,
    string TaskText,
    string? TaskHints,
    DateTime StartedAtUtc,
    DateTime? EndedAtUtc,
    int? FocusScorePercent,
    long? FocusedSeconds,
    long? DistractedSeconds,
    int? DistractionCount,
    int? ContextSwitchCostSeconds,
    string? TopDistractingApps,
    string Source);

/// <summary>Generic paginated response wrapper.</summary>
public sealed record PaginatedResponse<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize);

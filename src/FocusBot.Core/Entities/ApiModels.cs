namespace FocusBot.Core.Entities;

public sealed record EndSessionPayload(int FocusScorePercent, long FocusedSeconds, long DistractedSeconds, int DistractionCount, int ContextSwitchCostSeconds, string? TopDistractingApps);
public sealed record ClassifyPayload(string TaskText, string? TaskHints, string? ProcessName, string? WindowTitle);
public sealed record ApiSessionResponse(Guid Id, string TaskText, string? TaskHints, DateTime StartedAtUtc, DateTime? EndedAtUtc);
public sealed record ApiClassifyResponse(int Score, string Reason, bool Cached);
public sealed record ApiSubscriptionStatus(string Status, DateTime? TrialEndsAt, DateTime? CurrentPeriodEndsAt);

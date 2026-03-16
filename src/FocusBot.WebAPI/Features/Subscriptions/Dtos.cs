namespace FocusBot.WebAPI.Features.Subscriptions;

/// <summary>Response for the current subscription status.</summary>
public sealed record SubscriptionStatusResponse(string Status, DateTime? TrialEndsAt, DateTime? CurrentPeriodEndsAt);

/// <summary>Response after activating a 24-hour trial.</summary>
public sealed record ActivateTrialResponse(string Status, DateTime TrialEndsAt);

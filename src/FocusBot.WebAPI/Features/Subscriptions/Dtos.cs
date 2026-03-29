using FocusBot.WebAPI.Data.Entities;

namespace FocusBot.WebAPI.Features.Subscriptions;

/// <summary>Response for the current subscription status.</summary>
public sealed record SubscriptionStatusResponse(
    string Status,
    PlanType PlanType,
    DateTime? TrialEndsAt,
    DateTime? CurrentPeriodEndsAt,
    DateTime? NextBilledAtUtc = null);

/// <summary>Response for POST /subscriptions/portal.</summary>
public sealed record CustomerPortalResponse(string Url);

/// <summary>Response after activating a 24-hour trial.</summary>
public sealed record ActivateTrialResponse(string Status, DateTime TrialEndsAt);

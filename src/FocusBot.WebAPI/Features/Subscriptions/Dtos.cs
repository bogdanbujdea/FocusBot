using FocusBot.WebAPI.Data.Entities;

namespace FocusBot.WebAPI.Features.Subscriptions;

/// <summary>Response for the current subscription status.</summary>
public sealed record SubscriptionStatusResponse(
    SubscriptionStatus Status,
    PlanType PlanType,
    DateTime CurrentPeriodEndsAt,
    DateTime? NextBilledAtUtc = null
);

/// <summary>Response for POST /subscriptions/portal.</summary>
public sealed record CustomerPortalResponse(string Url);

/// <summary>Response after activating a 24-hour trial.</summary>
public sealed record ActivateTrialResponse(SubscriptionStatus Status, DateTime TrialEndsAt);

/// <summary>Outcome of POST /subscriptions/trial.</summary>
public enum ActivateTrialResultKind
{
    Created,
    AlreadyExists,
    UserNotProvisioned,
}

/// <summary>Result of trial activation including conflict and not-provisioned cases.</summary>
public sealed record ActivateTrialOutcome(
    ActivateTrialResultKind Kind,
    ActivateTrialResponse? Response = null
);

/// <summary>Request to activate a trial.</summary>
public sealed record ActivateTrialRequest(PlanType PlanType);

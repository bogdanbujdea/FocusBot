using FocusBot.WebAPI.Data.Entities;

namespace FocusBot.WebAPI.Features.Auth;

/// <summary>Response for GET /auth/me — returns the authenticated user's profile and current plan.</summary>
public sealed record MeResponse(Guid UserId, string Email, SubscriptionStatus SubscriptionStatus, PlanType PlanType);

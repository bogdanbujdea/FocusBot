namespace FocusBot.WebAPI.Features.Auth;

public sealed record MeResponse(Guid UserId, string Email, string SubscriptionStatus);

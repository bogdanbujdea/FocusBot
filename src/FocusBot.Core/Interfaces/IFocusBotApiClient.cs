using FocusBot.Core.Entities;

namespace FocusBot.Core.Interfaces;

public interface IFocusBotApiClient
{
    Task<ApiSessionResponse?> StartSessionAsync(string taskText, string? taskHints);
    Task<ApiSessionResponse?> EndSessionAsync(Guid sessionId, EndSessionPayload payload);
    Task<ApiClassifyResponse?> ClassifyAsync(ClassifyPayload payload);
    Task<ApiSubscriptionStatus?> GetSubscriptionStatusAsync();
    bool IsConfigured { get; }
}

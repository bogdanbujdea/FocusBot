using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Service implementation for session control (pause, resume, end).
/// Delegates to IFocusBotApiClient and owns the placeholder end payload.
/// </summary>
public class FocusSessionControlService(IFocusBotApiClient apiClient) : IFocusSessionControlService
{
    public async Task<ApiResult<ApiSessionResponse>> TogglePauseAsync(Guid sessionId, bool isCurrentlyPaused)
    {
        return isCurrentlyPaused
            ? await apiClient.ResumeSessionAsync(sessionId)
            : await apiClient.PauseSessionAsync(sessionId);
    }

    public async Task<ApiResult<ApiSessionResponse>> EndWithPlaceholderMetricsAsync(Guid sessionId)
    {
        var payload = new EndSessionPayload(0, 0, 0, 0, 0, null, null);
        return await apiClient.EndSessionAsync(sessionId, payload);
    }
}

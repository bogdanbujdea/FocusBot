using FocusBot.Core.Entities;

namespace FocusBot.Core.Interfaces;

/// <summary>
/// Service for controlling focus session lifecycle (pause, resume, end).
/// Abstracts session control logic from ViewModels.
/// </summary>
public interface IFocusSessionControlService
{
    /// <summary>
    /// Toggles pause state: resumes if currently paused, pauses if running.
    /// </summary>
    Task<ApiResult<ApiSessionResponse>> TogglePauseAsync(Guid sessionId, bool isCurrentlyPaused);

    /// <summary>
    /// Ends the session with placeholder metrics (zeros).
    /// </summary>
    Task<ApiResult<ApiSessionResponse>> EndWithPlaceholderMetricsAsync(Guid sessionId);
}

using FocusBot.Core.Entities;

namespace FocusBot.Core.Interfaces;

/// <summary>
/// Repository for managing user tasks (single active task + completed history).
/// </summary>
public interface ISessionRepository
{
    Task<UserSession> AddSessionAsync(string description, string? sessionContext = null);
    Task<UserSession?> GetByIdAsync(string sessionId);
    Task SetActiveAsync(string sessionId);
    Task SetCompletedAsync(string sessionId);
    Task UpdateElapsedTimeAsync(string sessionId, long totalElapsedSeconds);
    Task<UserSession?> GetInProgressSessionAsync();
    Task<IEnumerable<UserSession>> GetDoneSessionsAsync();
    Task UpdateFocusScoreAsync(string sessionId, int scorePercent);
}

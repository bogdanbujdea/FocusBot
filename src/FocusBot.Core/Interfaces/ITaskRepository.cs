using FocusBot.Core.Entities;

namespace FocusBot.Core.Interfaces;

/// <summary>
/// Repository for managing user tasks (single active task + completed history).
/// </summary>
public interface ITaskRepository
{
    Task<UserTask> AddTaskAsync(string description, string? taskContext = null);
    Task<UserTask?> GetByIdAsync(string taskId);
    Task SetActiveAsync(string taskId);
    Task SetCompletedAsync(string taskId);
    Task UpdateElapsedTimeAsync(string taskId, long totalElapsedSeconds);
    Task<UserTask?> GetInProgressTaskAsync();
    Task<IEnumerable<UserTask>> GetDoneTasksAsync();
    Task UpdateFocusScoreAsync(string taskId, int scorePercent);
    Task UpdateTaskSummaryAsync(
        string taskId,
        long focusedSeconds,
        long distractedSeconds,
        int distractionCount,
        int contextSwitchCount,
        string? topDistractingApps,
        string? topAlignedApps
    );
}

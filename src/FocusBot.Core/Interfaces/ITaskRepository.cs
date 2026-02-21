using FocusBot.Core.Entities;

namespace FocusBot.Core.Interfaces;

/// <summary>
/// Repository for managing user tasks in a Kanban board.
/// </summary>
public interface ITaskRepository
{
    Task<UserTask> AddTaskAsync(string description, string? taskContext = null);
    Task<UserTask?> GetByIdAsync(string taskId);
    Task UpdateTaskDescriptionAsync(string taskId, string newDescription);
    Task UpdateTaskAsync(string taskId, string description, string? taskContext);
    Task DeleteTaskAsync(string taskId);
    Task SetStatusToAsync(string taskId, Entities.TaskStatus status);
    Task UpdateElapsedTimeAsync(string taskId, long totalElapsedSeconds);
    Task<IEnumerable<UserTask>> GetToDoTasksAsync();
    Task<UserTask?> GetInProgressTaskAsync();
    Task<IEnumerable<UserTask>> GetDoneTasksAsync();
    Task UpsertFocusSegmentsAsync(IEnumerable<FocusSegment> segments);
    Task<IEnumerable<FocusSegment>> GetFocusSegmentsForTaskAsync(string taskId);
    Task UpdateFocusScoreAsync(string taskId, int scorePercent);
    Task DeleteFocusSegmentsForTaskAsync(string taskId);
}

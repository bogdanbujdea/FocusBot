using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using TaskStatus = FocusBot.Core.Entities.TaskStatus;

namespace FocusBot.Infrastructure.Data;

public class TaskRepository(AppDbContext context) : ITaskRepository
{
    public async Task<UserTask> AddTaskAsync(string description)
    {
        var task = new UserTask
        {
            TaskId = Guid.NewGuid().ToString(),
            Description = description,
            Status = TaskStatus.ToDo,
        };
        context.UserTasks.Add(task);
        await context.SaveChangesAsync();
        return task;
    }

    public async Task<UserTask?> GetByIdAsync(string taskId) =>
        await context.UserTasks.FindAsync(taskId);

    public async Task UpdateTaskDescriptionAsync(string taskId, string newDescription)
    {
        var task = await context.UserTasks.FindAsync(taskId);
        if (task != null)
        {
            task.Description = newDescription;
            await context.SaveChangesAsync();
        }
    }

    public async Task DeleteTaskAsync(string taskId)
    {
        var task = await context.UserTasks.FindAsync(taskId);
        if (task != null)
        {
            context.UserTasks.Remove(task);
            await context.SaveChangesAsync();
        }
    }

    public async Task SetStatusToAsync(string taskId, TaskStatus status)
    {
        if (status == TaskStatus.InProgress)
        {
            var existing = await context.UserTasks
                .Where(t => t.Status == TaskStatus.InProgress)
                .FirstOrDefaultAsync();
            if (IsDifferentTask(existing, taskId))
                existing!.Status = TaskStatus.ToDo;
        }

        var task = await context.UserTasks.FindAsync(taskId);
        if (task != null)
        {
            task.Status = status;
            await context.SaveChangesAsync();
        }
    }

    public async Task UpdateElapsedTimeAsync(string taskId, long totalElapsedSeconds)
    {
        var task = await context.UserTasks.FindAsync(taskId);
        if (task != null)
        {
            task.TotalElapsedSeconds = totalElapsedSeconds;
            await context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<UserTask>> GetToDoTasksAsync() =>
        await context.UserTasks
            .Where(t => t.Status == TaskStatus.ToDo)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

    public async Task<UserTask?> GetInProgressTaskAsync() =>
        await context.UserTasks
            .Where(t => t.Status == TaskStatus.InProgress)
            .FirstOrDefaultAsync();

    public async Task<IEnumerable<UserTask>> GetDoneTasksAsync() =>
        await context.UserTasks
            .Where(t => t.Status == TaskStatus.Done)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

    private static bool IsDifferentTask(UserTask? existing, string taskId) =>
        existing != null && existing.TaskId != taskId;
}

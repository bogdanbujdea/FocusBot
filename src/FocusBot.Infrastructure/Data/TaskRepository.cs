using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FocusBot.Infrastructure.Data;

public class TaskRepository(AppDbContext context) : ITaskRepository
{
    public async Task<UserTask> AddTaskAsync(string description, string? taskContext = null)
    {
        var task = new UserTask
        {
            TaskId = Guid.NewGuid().ToString(),
            Description = description,
            Context = string.IsNullOrWhiteSpace(taskContext) ? null : taskContext.Trim(),
            IsCompleted = false,
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

    public async Task UpdateTaskAsync(string taskId, string description, string? taskContext)
    {
        var task = await context.UserTasks.FindAsync(taskId);
        if (task != null)
        {
            task.Description = description;
            task.Context = string.IsNullOrWhiteSpace(taskContext) ? null : taskContext.Trim();
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

    public async Task SetActiveAsync(string taskId)
    {
        var othersInProgress = await context.UserTasks
            .Where(t => !t.IsCompleted && t.TaskId != taskId)
            .ToListAsync();
        foreach (var t in othersInProgress)
        {
            t.IsCompleted = true;
        }

        var task = await context.UserTasks.FindAsync(taskId);
        if (task != null)
        {
            task.IsCompleted = false;
            await context.SaveChangesAsync();
        }
    }

    public async Task SetCompletedAsync(string taskId)
    {
        var task = await context.UserTasks.FindAsync(taskId);
        if (task != null)
        {
            task.IsCompleted = true;
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

    public async Task<UserTask?> GetInProgressTaskAsync() =>
        await context.UserTasks
            .Where(t => !t.IsCompleted)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();

    public async Task<IEnumerable<UserTask>> GetDoneTasksAsync() =>
        await context.UserTasks
            .Where(t => t.IsCompleted)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

    public async Task UpdateFocusScoreAsync(string taskId, int scorePercent)
    {
        var task = await context.UserTasks.FindAsync(taskId);
        if (task != null)
        {
            task.FocusScorePercent = scorePercent;
            await context.SaveChangesAsync();
        }
    }

    public async Task UpdateTaskSummaryAsync(string taskId, long focusedSeconds, long distractedSeconds, int distractionCount, int contextSwitchCount, string? topDistractingApps, string? topAlignedApps)
    {
        var task = await context.UserTasks.FindAsync(taskId);
        if (task != null)
        {
            task.FocusedSeconds = focusedSeconds;
            task.DistractedSeconds = distractedSeconds;
            task.DistractionCount = distractionCount;
            task.ContextSwitchCount = contextSwitchCount;
            task.TopDistractingApps = topDistractingApps;
            task.TopAlignedApps = topAlignedApps;
            await context.SaveChangesAsync();
        }
    }

}

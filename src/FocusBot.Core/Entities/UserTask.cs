namespace FocusBot.Core.Entities;

/// <summary>
/// Represents a user-defined task on the Kanban board.
/// </summary>
public class UserTask
{
    public UserTask() { }

    public string TaskId { get; set; } = Guid.NewGuid().ToString();
    public string Description { get; set; } = string.Empty;
    public string? Context { get; set; }
    public TaskStatus Status { get; set; } = TaskStatus.ToDo;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive => Status == TaskStatus.InProgress;
}

using TaskStatus = FocusBot.Core.Entities.TaskStatus;

namespace FocusBot.Infrastructure.Tests.Data.TaskRepositoryTests;

public class SetStatusToAsyncShould : TaskRepositoryTestBase
{
    [Fact]
    public async Task SetTaskStatusToToDo()
    {
        // Arrange
        var task = await Repository.AddTaskAsync("Pause this");
        await Repository.SetStatusToAsync(task.TaskId, TaskStatus.InProgress);

        // Act
        await Repository.SetStatusToAsync(task.TaskId, TaskStatus.ToDo);
        var fromDb = await Context.UserTasks.FindAsync(task.TaskId);

        // Assert
        fromDb.Should().NotBeNull();
        fromDb!.Status.Should().Be(TaskStatus.ToDo);
    }

    [Fact]
    public async Task SetTaskStatusToInProgress()
    {
        // Arrange
        var task = await Repository.AddTaskAsync("Work on this");

        // Act
        await Repository.SetStatusToAsync(task.TaskId, TaskStatus.InProgress);
        var fromDb = await Context.UserTasks.FindAsync(task.TaskId);

        // Assert
        fromDb.Should().NotBeNull();
        fromDb!.Status.Should().Be(TaskStatus.InProgress);
        fromDb.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task MovePreviousInProgressTaskBackToToDo_WhenSettingAnotherToInProgress()
    {
        // Arrange
        var first = await Repository.AddTaskAsync("First");
        await Repository.SetStatusToAsync(first.TaskId, TaskStatus.InProgress);
        var second = await Repository.AddTaskAsync("Second");

        // Act
        await Repository.SetStatusToAsync(second.TaskId, TaskStatus.InProgress);
        var firstUpdated = await Context.UserTasks.FindAsync(first.TaskId);
        var secondUpdated = await Context.UserTasks.FindAsync(second.TaskId);

        // Assert
        firstUpdated.Should().NotBeNull();
        firstUpdated!.Status.Should().Be(TaskStatus.ToDo);
        secondUpdated.Should().NotBeNull();
        secondUpdated!.Status.Should().Be(TaskStatus.InProgress);
    }

    [Fact]
    public async Task SetTaskStatusToDone()
    {
        // Arrange
        var task = await Repository.AddTaskAsync("Finish this");
        await Repository.SetStatusToAsync(task.TaskId, TaskStatus.InProgress);

        // Act
        await Repository.SetStatusToAsync(task.TaskId, TaskStatus.Done);
        var fromDb = await Context.UserTasks.FindAsync(task.TaskId);

        // Assert
        fromDb.Should().NotBeNull();
        fromDb!.Status.Should().Be(TaskStatus.Done);
    }

    [Fact]
    public async Task DoNothing_WhenTaskIdNotFound()
    {
        // Arrange
        var taskId = Guid.NewGuid().ToString();

        // Act
        await Repository.SetStatusToAsync(taskId, TaskStatus.Done);

        // Assert
        Context.UserTasks.Should().BeEmpty();
    }
}

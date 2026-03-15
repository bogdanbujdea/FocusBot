namespace FocusBot.Infrastructure.Tests.Data.TaskRepositoryTests;

public class SetActiveAsyncAndSetCompletedAsyncShould : TaskRepositoryTestBase
{
    [Fact]
    public async Task SetActiveAsync_MakesTaskActive()
    {
        // Arrange
        var task = await Repository.AddTaskAsync("Work on this");

        // Act
        await Repository.SetActiveAsync(task.TaskId);
        var fromDb = await Context.UserTasks.FindAsync(task.TaskId);

        // Assert
        fromDb.Should().NotBeNull();
        fromDb!.IsCompleted.Should().BeFalse();
        fromDb.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SetActiveAsync_MovesPreviousActiveTaskToCompleted()
    {
        // Arrange
        var first = await Repository.AddTaskAsync("First");
        await Repository.SetActiveAsync(first.TaskId);
        var second = await Repository.AddTaskAsync("Second");

        // Act
        await Repository.SetActiveAsync(second.TaskId);
        var firstUpdated = await Context.UserTasks.FindAsync(first.TaskId);
        var secondUpdated = await Context.UserTasks.FindAsync(second.TaskId);

        // Assert
        firstUpdated.Should().NotBeNull();
        firstUpdated!.IsCompleted.Should().BeTrue();
        secondUpdated.Should().NotBeNull();
        secondUpdated!.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task SetCompletedAsync_MarksTaskCompleted()
    {
        // Arrange
        var task = await Repository.AddTaskAsync("Finish this");
        await Repository.SetActiveAsync(task.TaskId);

        // Act
        await Repository.SetCompletedAsync(task.TaskId);
        var fromDb = await Context.UserTasks.FindAsync(task.TaskId);

        // Assert
        fromDb.Should().NotBeNull();
        fromDb!.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task SetCompletedAsync_DoesNothing_WhenTaskIdNotFound()
    {
        // Arrange
        var taskId = Guid.NewGuid().ToString();

        // Act
        await Repository.SetCompletedAsync(taskId);

        // Assert
        Context.UserTasks.Should().BeEmpty();
    }

    [Fact]
    public async Task SetActiveAsync_DoesNothing_WhenTaskIdNotFound()
    {
        // Arrange
        var taskId = Guid.NewGuid().ToString();

        // Act
        await Repository.SetActiveAsync(taskId);

        // Assert
        Context.UserTasks.Should().BeEmpty();
    }
}

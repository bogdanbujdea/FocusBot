namespace FocusBot.Infrastructure.Tests.Data.TaskRepositoryTests;

public class UpdateTaskDescriptionAsyncShould : TaskRepositoryTestBase
{
    [Fact]
    public async Task PersistNewDescription()
    {
        // Arrange
        var task = await Repository.AddTaskAsync("Original");

        // Act
        await Repository.UpdateTaskDescriptionAsync(task.TaskId, "Updated");
        var fromDb = await Context.UserTasks.FindAsync(task.TaskId);

        // Assert
        fromDb.Should().NotBeNull();
        fromDb!.Description.Should().Be("Updated");
    }

    [Fact]
    public async Task DoNothing_WhenTaskIdNotFound()
    {
        // Arrange
        var taskId = Guid.NewGuid().ToString();

        // Act
        await Repository.UpdateTaskDescriptionAsync(taskId, "No-op");

        // Assert
        Context.UserTasks.Should().BeEmpty();
    }
}

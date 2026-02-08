namespace FocusBot.Infrastructure.Tests.Data.TaskRepositoryTests;

public class DeleteTaskAsyncShould : TaskRepositoryTestBase
{
    [Fact]
    public async Task RemoveTaskFromStore()
    {
        // Arrange
        var task = await Repository.AddTaskAsync("To delete");

        // Act
        await Repository.DeleteTaskAsync(task.TaskId);
        var found = await Repository.GetByIdAsync(task.TaskId);

        // Assert
        found.Should().BeNull();
    }

    [Fact]
    public async Task NotThrow_WhenTaskIdNotFound()
    {
        // Arrange
        var taskId = Guid.NewGuid().ToString();

        // Act
        var act = async () => await Repository.DeleteTaskAsync(taskId);

        // Assert
        await act.Should().NotThrowAsync();
    }
}

namespace FocusBot.Infrastructure.Tests.Data.TaskRepositoryTests;

public class DeleteTaskAsyncShould : TaskRepositoryTestBase
{
    [Fact]
    public async Task RemoveTaskFromStore()
    {
        // Arrange
        var task = await Repository.AddSessionAsync("To delete");

        // Act
        await Repository.DeleteSessionAsync(task.SessionId);
        var found = await Repository.GetByIdAsync(task.SessionId);

        // Assert
        found.Should().BeNull();
    }

    [Fact]
    public async Task NotThrow_WhenTaskIdNotFound()
    {
        // Arrange
        var taskId = Guid.NewGuid().ToString();

        // Act
        var act = async () => await Repository.DeleteSessionAsync(taskId);

        // Assert
        await act.Should().NotThrowAsync();
    }
}

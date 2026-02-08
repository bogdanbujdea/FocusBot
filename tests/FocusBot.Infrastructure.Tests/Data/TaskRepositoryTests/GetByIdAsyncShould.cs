namespace FocusBot.Infrastructure.Tests.Data.TaskRepositoryTests;

public class GetByIdAsyncShould : TaskRepositoryTestBase
{
    [Fact]
    public async Task ReturnTask_WhenIdExists()
    {
        // Arrange
        var created = await Repository.AddTaskAsync("Find me");

        // Act
        var found = await Repository.GetByIdAsync(created.TaskId);

        // Assert
        found.Should().NotBeNull();
        found!.TaskId.Should().Be(created.TaskId);
        found.Description.Should().Be("Find me");
    }

    [Fact]
    public async Task ReturnNull_WhenIdDoesNotExist()
    {
        // Arrange
        var taskId = Guid.NewGuid().ToString();

        // Act
        var found = await Repository.GetByIdAsync(taskId);

        // Assert
        found.Should().BeNull();
    }
}

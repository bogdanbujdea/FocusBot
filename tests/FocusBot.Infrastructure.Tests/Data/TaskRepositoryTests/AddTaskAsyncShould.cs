namespace FocusBot.Infrastructure.Tests.Data.TaskRepositoryTests;

public class AddTaskAsyncShould : TaskRepositoryTestBase
{
    [Fact]
    public async Task CreateActiveTaskWithDescription()
    {
        // Arrange
        // (no setup beyond base)

        // Act
        var task = await Repository.AddSessionAsync("Ship the feature");

        // Assert
        task.Should().NotBeNull();
        task!.Description.Should().Be("Ship the feature");
        task.IsCompleted.Should().BeFalse();
        Guid.TryParse(task.SessionId, out _).Should().BeTrue();
    }
}

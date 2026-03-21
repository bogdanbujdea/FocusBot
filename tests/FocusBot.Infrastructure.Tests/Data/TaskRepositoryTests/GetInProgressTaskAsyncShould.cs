namespace FocusBot.Infrastructure.Tests.Data.TaskRepositoryTests;

public class GetInProgressTaskAsyncShould : TaskRepositoryTestBase
{
    [Fact]
    public async Task ReturnNull_WhenNoActiveTask()
    {
        // Arrange
        var task = await Repository.AddSessionAsync("Only task");
        await Repository.SetCompletedAsync(task.SessionId);

        // Act
        var result = await Repository.GetInProgressSessionAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ReturnTheActiveTask()
    {
        // Arrange
        var task = await Repository.AddSessionAsync("Active");
        await Repository.SetActiveAsync(task.SessionId);

        // Act
        var inProgress = await Repository.GetInProgressSessionAsync();

        // Assert
        inProgress.Should().NotBeNull();
        inProgress!.SessionId.Should().Be(task.SessionId);
        inProgress.SessionTitle.Should().Be("Active");
    }
}

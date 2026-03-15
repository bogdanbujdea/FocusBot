namespace FocusBot.Infrastructure.Tests.Data.TaskRepositoryTests;

public class GetInProgressTaskAsyncShould : TaskRepositoryTestBase
{
    [Fact]
    public async Task ReturnNull_WhenNoActiveTask()
    {
        // Arrange
        var task = await Repository.AddTaskAsync("Only task");
        await Repository.SetCompletedAsync(task.TaskId);

        // Act
        var result = await Repository.GetInProgressTaskAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ReturnTheActiveTask()
    {
        // Arrange
        var task = await Repository.AddTaskAsync("Active");
        await Repository.SetActiveAsync(task.TaskId);

        // Act
        var inProgress = await Repository.GetInProgressTaskAsync();

        // Assert
        inProgress.Should().NotBeNull();
        inProgress!.TaskId.Should().Be(task.TaskId);
        inProgress.Description.Should().Be("Active");
    }
}

using TaskStatus = FocusBot.Core.Entities.TaskStatus;

namespace FocusBot.Infrastructure.Tests.Data.TaskRepositoryTests;

public class GetInProgressTaskAsyncShould : TaskRepositoryTestBase
{
    [Fact]
    public async Task ReturnNull_WhenNoTaskIsInProgress()
    {
        // Arrange
        await Repository.AddTaskAsync("ToDo only");

        // Act
        var result = await Repository.GetInProgressTaskAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ReturnTheInProgressTask()
    {
        // Arrange
        var task = await Repository.AddTaskAsync("Active");
        await Repository.SetStatusToAsync(task.TaskId, TaskStatus.InProgress);

        // Act
        var inProgress = await Repository.GetInProgressTaskAsync();

        // Assert
        inProgress.Should().NotBeNull();
        inProgress!.TaskId.Should().Be(task.TaskId);
        inProgress.Description.Should().Be("Active");
    }
}

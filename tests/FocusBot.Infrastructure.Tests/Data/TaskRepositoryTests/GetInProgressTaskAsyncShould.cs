using TaskStatus = FocusBot.Core.Entities.TaskStatus;

namespace FocusBot.Infrastructure.Tests.Data.TaskRepositoryTests;

public class GetInProgressTaskAsyncShould : TaskRepositoryTestBase
{
    [Fact]
    public async Task ReturnNull_WhenNoTaskIsInProgress()
    {
        await Repository.AddTaskAsync("ToDo only");
        var result = await Repository.GetInProgressTaskAsync();
        result.Should().BeNull();
    }

    [Fact]
    public async Task ReturnTheInProgressTask()
    {
        var task = await Repository.AddTaskAsync("Active");
        await Repository.SetStatusToAsync(task.TaskId, TaskStatus.InProgress);
        var inProgress = await Repository.GetInProgressTaskAsync();
        inProgress.Should().NotBeNull();
        inProgress.TaskId.Should().Be(task.TaskId);
        inProgress.Description.Should().Be("Active");
    }
}

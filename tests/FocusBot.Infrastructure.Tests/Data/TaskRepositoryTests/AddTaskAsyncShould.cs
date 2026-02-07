using TaskStatus = FocusBot.Core.Entities.TaskStatus;

namespace FocusBot.Infrastructure.Tests.Data.TaskRepositoryTests;

public class AddTaskAsyncShould : TaskRepositoryTestBase
{
    [Fact]
    public async Task CreateToDoTaskWithDescription()
    {
        var task = await Repository.AddTaskAsync("Ship the feature");

        task.Should().NotBeNull();
        task.Description.Should().Be("Ship the feature");
        task.Status.Should().Be(TaskStatus.ToDo);
        Guid.TryParse(task.TaskId, out _).Should().BeTrue();
    }
}

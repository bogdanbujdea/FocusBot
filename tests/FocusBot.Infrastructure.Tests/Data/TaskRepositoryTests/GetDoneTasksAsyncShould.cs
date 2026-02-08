using TaskStatus = FocusBot.Core.Entities.TaskStatus;

namespace FocusBot.Infrastructure.Tests.Data.TaskRepositoryTests;

public class GetDoneTasksAsyncShould : TaskRepositoryTestBase
{
    [Fact]
    public async Task ReturnOnlyDoneTasks()
    {
        // Arrange
        var t1 = await Repository.AddTaskAsync("Done 1");
        await Repository.SetStatusToAsync(t1.TaskId, TaskStatus.InProgress);
        await Repository.SetStatusToAsync(t1.TaskId, TaskStatus.Done);
        var t2 = await Repository.AddTaskAsync("Done 2");
        await Repository.SetStatusToAsync(t2.TaskId, TaskStatus.InProgress);
        await Repository.SetStatusToAsync(t2.TaskId, TaskStatus.Done);
        await Repository.AddTaskAsync("Still ToDo");

        // Act
        var done = await Repository.GetDoneTasksAsync();
        var userTasks = done.ToList();

        // Assert
        userTasks.Should().HaveCount(2);
        userTasks.Should().OnlyContain(t => t.Status == TaskStatus.Done);
    }

    [Fact]
    public async Task ReturnEmpty_WhenNoDoneTasks()
    {
        // Arrange
        // (no tasks in store)

        // Act
        var done = await Repository.GetDoneTasksAsync();

        // Assert
        done.Should().BeEmpty();
    }
}

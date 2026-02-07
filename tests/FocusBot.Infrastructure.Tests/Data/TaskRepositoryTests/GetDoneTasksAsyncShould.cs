using TaskStatus = FocusBot.Core.Entities.TaskStatus;

namespace FocusBot.Infrastructure.Tests.Data.TaskRepositoryTests;

public class GetDoneTasksAsyncShould : TaskRepositoryTestBase
{
    [Fact]
    public async Task ReturnOnlyDoneTasks()
    {
        var t1 = await Repository.AddTaskAsync("Done 1");
        await Repository.SetStatusToAsync(t1.TaskId, TaskStatus.InProgress);
        await Repository.SetStatusToAsync(t1.TaskId, TaskStatus.Done);
        var t2 = await Repository.AddTaskAsync("Done 2");
        await Repository.SetStatusToAsync(t2.TaskId, TaskStatus.InProgress);
        await Repository.SetStatusToAsync(t2.TaskId, TaskStatus.Done);
        await Repository.AddTaskAsync("Still ToDo");

        var done = await Repository.GetDoneTasksAsync();
        var userTasks = done.ToList();
        userTasks.Should().HaveCount(2);
        userTasks.Should().OnlyContain(t => t.Status == TaskStatus.Done);
    }

    [Fact]
    public async Task ReturnEmpty_WhenNoDoneTasks()
    {
        var done = await Repository.GetDoneTasksAsync();
        done.Should().BeEmpty();
    }
}

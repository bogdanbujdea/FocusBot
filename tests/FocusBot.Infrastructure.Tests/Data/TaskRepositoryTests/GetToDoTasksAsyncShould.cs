using TaskStatus = FocusBot.Core.Entities.TaskStatus;

namespace FocusBot.Infrastructure.Tests.Data.TaskRepositoryTests;

public class GetToDoTasksAsyncShould : TaskRepositoryTestBase
{
    [Fact]
    public async Task ReturnOnlyToDoTasks_ExcludingInProgressAndDone()
    {
        await Repository.AddTaskAsync("ToDo 1");
        await Repository.AddTaskAsync("ToDo 2");
        var inProgress = await Repository.AddTaskAsync("In Progress");
        await Repository.SetStatusToAsync(inProgress.TaskId, TaskStatus.InProgress);
        var done = await Repository.AddTaskAsync("Done");
        await Repository.SetStatusToAsync(done.TaskId, TaskStatus.InProgress);
        await Repository.SetStatusToAsync(done.TaskId, TaskStatus.Done);

        var toDo = await Repository.GetToDoTasksAsync();

        var userTasks = toDo.ToList();
        userTasks.Should().HaveCount(3);
        userTasks.Should().OnlyContain(t => t.Status == TaskStatus.ToDo);
    }

    [Fact]
    public async Task ReturnEmpty_WhenNoToDoTasks()
    {
        var toDo = await Repository.GetToDoTasksAsync();

        toDo.Should().BeEmpty();
    }

    [Fact]
    public async Task ReturnTasksOrderedByCreatedAtDescending()
    {
        await Repository.AddTaskAsync("First");
        await Repository.AddTaskAsync("Second");

        var toDo = (await Repository.GetToDoTasksAsync()).ToList();

        toDo.Should().HaveCount(2);
        toDo[0].CreatedAt.Should().BeAfter(toDo[1].CreatedAt);
    }
}

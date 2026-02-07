using TaskStatus = FocusBot.Core.Entities.TaskStatus;

namespace FocusBot.Infrastructure.Tests.Data.TaskRepositoryTests;

public class SetStatusToAsyncShould : TaskRepositoryTestBase
{
    [Fact]
    public async Task SetTaskStatusToToDo()
    {
        var task = await Repository.AddTaskAsync("Pause this");
        await Repository.SetStatusToAsync(task.TaskId, TaskStatus.InProgress);
        await Repository.SetStatusToAsync(task.TaskId, TaskStatus.ToDo);

        var fromDb = await Context.UserTasks.FindAsync(task.TaskId);
        fromDb.Should().NotBeNull();
        fromDb!.Status.Should().Be(TaskStatus.ToDo);
    }

    [Fact]
    public async Task SetTaskStatusToInProgress()
    {
        var task = await Repository.AddTaskAsync("Work on this");
        await Repository.SetStatusToAsync(task.TaskId, TaskStatus.InProgress);

        var fromDb = await Context.UserTasks.FindAsync(task.TaskId);
        fromDb.Should().NotBeNull();
        fromDb!.Status.Should().Be(TaskStatus.InProgress);
        fromDb.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task MovePreviousInProgressTaskBackToToDo_WhenSettingAnotherToInProgress()
    {
        var first = await Repository.AddTaskAsync("First");
        await Repository.SetStatusToAsync(first.TaskId, TaskStatus.InProgress);
        var second = await Repository.AddTaskAsync("Second");
        await Repository.SetStatusToAsync(second.TaskId, TaskStatus.InProgress);

        var firstUpdated = await Context.UserTasks.FindAsync(first.TaskId);
        firstUpdated.Should().NotBeNull();
        firstUpdated!.Status.Should().Be(TaskStatus.ToDo);

        var secondUpdated = await Context.UserTasks.FindAsync(second.TaskId);
        secondUpdated.Should().NotBeNull();
        secondUpdated!.Status.Should().Be(TaskStatus.InProgress);
    }

    [Fact]
    public async Task SetTaskStatusToDone()
    {
        var task = await Repository.AddTaskAsync("Finish this");
        await Repository.SetStatusToAsync(task.TaskId, TaskStatus.InProgress);
        await Repository.SetStatusToAsync(task.TaskId, TaskStatus.Done);

        var fromDb = await Context.UserTasks.FindAsync(task.TaskId);
        fromDb.Should().NotBeNull();
        fromDb!.Status.Should().Be(TaskStatus.Done);
    }

    [Fact]
    public async Task DoNothing_WhenTaskIdNotFound()
    {
        await Repository.SetStatusToAsync(Guid.NewGuid().ToString(), TaskStatus.Done);
        Context.UserTasks.Should().BeEmpty();
    }
}

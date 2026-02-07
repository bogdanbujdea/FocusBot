namespace FocusBot.Infrastructure.Tests.Data.TaskRepositoryTests;

public class UpdateTaskDescriptionAsyncShould : TaskRepositoryTestBase
{
    [Fact]
    public async Task PersistNewDescription()
    {
        var task = await Repository.AddTaskAsync("Original");

        await Repository.UpdateTaskDescriptionAsync(task.TaskId, "Updated");

        var fromDb = await Context.UserTasks.FindAsync(task.TaskId);
        fromDb.Should().NotBeNull();
        fromDb.Description.Should().Be("Updated");
    }

    [Fact]
    public async Task DoNothing_WhenTaskIdNotFound()
    {
        await Repository.UpdateTaskDescriptionAsync(Guid.NewGuid().ToString(), "No-op");
        Context.UserTasks.Should().BeEmpty();
    }
}

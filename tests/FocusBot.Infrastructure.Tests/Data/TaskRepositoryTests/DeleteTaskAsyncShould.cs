namespace FocusBot.Infrastructure.Tests.Data.TaskRepositoryTests;

public class DeleteTaskAsyncShould : TaskRepositoryTestBase
{
    [Fact]
    public async Task RemoveTaskFromStore()
    {
        var task = await Repository.AddTaskAsync("To delete");
        await Repository.DeleteTaskAsync(task.TaskId);
        var found = await Repository.GetByIdAsync(task.TaskId);
        found.Should().BeNull();
    }

    [Fact]
    public async Task NotThrow_WhenTaskIdNotFound()
    {
        var act = async () => await Repository.DeleteTaskAsync(Guid.NewGuid().ToString());
        await act.Should().NotThrowAsync();
    }
}

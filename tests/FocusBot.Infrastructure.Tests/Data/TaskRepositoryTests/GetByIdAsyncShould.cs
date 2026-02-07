namespace FocusBot.Infrastructure.Tests.Data.TaskRepositoryTests;

public class GetByIdAsyncShould : TaskRepositoryTestBase
{
    [Fact]
    public async Task ReturnTask_WhenIdExists()
    {
        var created = await Repository.AddTaskAsync("Find me");
        var found = await Repository.GetByIdAsync(created.TaskId);
        found.Should().NotBeNull();
        found.TaskId.Should().Be(created.TaskId);
        found.Description.Should().Be("Find me");
    }

    [Fact]
    public async Task ReturnNull_WhenIdDoesNotExist()
    {
        var found = await Repository.GetByIdAsync(Guid.NewGuid().ToString());
        found.Should().BeNull();
    }
}

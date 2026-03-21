namespace FocusBot.Infrastructure.Tests.Data.TaskRepositoryTests;

public class GetDoneTasksAsyncShould : TaskRepositoryTestBase
{
    [Fact]
    public async Task ReturnOnlyCompletedTasks()
    {
        // Arrange
        var t1 = await Repository.AddSessionAsync("Done 1");
        await Repository.SetActiveAsync(t1.SessionId);
        await Repository.SetCompletedAsync(t1.SessionId);
        var t2 = await Repository.AddSessionAsync("Done 2");
        await Repository.SetActiveAsync(t2.SessionId);
        await Repository.SetCompletedAsync(t2.SessionId);
        await Repository.AddSessionAsync("Still active");

        // Act
        var done = await Repository.GetDoneSessionsAsync();
        var userTasks = done.ToList();

        // Assert
        userTasks.Should().HaveCount(2);
        userTasks.Should().OnlyContain(t => t.IsCompleted);
    }

    [Fact]
    public async Task ReturnEmpty_WhenNoDoneTasks()
    {
        // Arrange
        // (no tasks in store)

        // Act
        var done = await Repository.GetDoneSessionsAsync();

        // Assert
        done.Should().BeEmpty();
    }
}

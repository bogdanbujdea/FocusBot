namespace FocusBot.Infrastructure.Tests.Data.TaskRepositoryTests;

public class GetDoneTasksAsyncShould : TaskRepositoryTestBase
{
    [Fact]
    public async Task ReturnOnlyCompletedTasks()
    {
        // Arrange
        var t1 = await Repository.AddTaskAsync("Done 1");
        await Repository.SetActiveAsync(t1.TaskId);
        await Repository.SetCompletedAsync(t1.TaskId);
        var t2 = await Repository.AddTaskAsync("Done 2");
        await Repository.SetActiveAsync(t2.TaskId);
        await Repository.SetCompletedAsync(t2.TaskId);
        await Repository.AddTaskAsync("Still active");

        // Act
        var done = await Repository.GetDoneTasksAsync();
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
        var done = await Repository.GetDoneTasksAsync();

        // Assert
        done.Should().BeEmpty();
    }
}

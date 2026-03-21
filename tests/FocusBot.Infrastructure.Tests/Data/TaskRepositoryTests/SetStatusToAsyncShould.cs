namespace FocusBot.Infrastructure.Tests.Data.TaskRepositoryTests;

public class SetActiveAsyncAndSetCompletedAsyncShould : TaskRepositoryTestBase
{
    [Fact]
    public async Task SetActiveAsync_MakesTaskActive()
    {
        // Arrange
        var task = await Repository.AddSessionAsync("Work on this");

        // Act
        await Repository.SetActiveAsync(task.SessionId);
        var fromDb = await Context.UserSessions.FindAsync(task.SessionId);

        // Assert
        fromDb.Should().NotBeNull();
        fromDb!.IsCompleted.Should().BeFalse();
        fromDb.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SetActiveAsync_MovesPreviousActiveTaskToCompleted()
    {
        // Arrange
        var first = await Repository.AddSessionAsync("First");
        await Repository.SetActiveAsync(first.SessionId);
        var second = await Repository.AddSessionAsync("Second");

        // Act
        await Repository.SetActiveAsync(second.SessionId);
        var firstUpdated = await Context.UserSessions.FindAsync(first.SessionId);
        var secondUpdated = await Context.UserSessions.FindAsync(second.SessionId);

        // Assert
        firstUpdated.Should().NotBeNull();
        firstUpdated!.IsCompleted.Should().BeTrue();
        secondUpdated.Should().NotBeNull();
        secondUpdated!.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task SetCompletedAsync_MarksTaskCompleted()
    {
        // Arrange
        var task = await Repository.AddSessionAsync("Finish this");
        await Repository.SetActiveAsync(task.SessionId);

        // Act
        await Repository.SetCompletedAsync(task.SessionId);
        var fromDb = await Context.UserSessions.FindAsync(task.SessionId);

        // Assert
        fromDb.Should().NotBeNull();
        fromDb!.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task SetCompletedAsync_DoesNothing_WhenTaskIdNotFound()
    {
        // Arrange
        var taskId = Guid.NewGuid().ToString();

        // Act
        await Repository.SetCompletedAsync(taskId);

        // Assert
        Context.UserSessions.Should().BeEmpty();
    }

    [Fact]
    public async Task SetActiveAsync_DoesNothing_WhenTaskIdNotFound()
    {
        // Arrange
        var taskId = Guid.NewGuid().ToString();

        // Act
        await Repository.SetActiveAsync(taskId);

        // Assert
        Context.UserSessions.Should().BeEmpty();
    }
}

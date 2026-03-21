using FocusBot.Core.Entities;

namespace FocusBot.Core.Tests.Entities.UserTaskTests;

public class NewTaskShould
{
    [Fact]
    public void HaveNonEmptyTaskId()
    {
        // Arrange
        var task = new UserSession();

        // Act
        var taskId = task.SessionId;

        // Assert
        taskId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void HaveParseableGuidTaskId()
    {
        // Arrange
        var task = new UserSession();

        // Act
        var parseable = Guid.TryParse(task.SessionId, out _);

        // Assert
        parseable.Should().BeTrue();
    }

    [Fact]
    public void DefaultIsCompletedToFalse()
    {
        // Arrange
        var task = new UserSession();

        // Act
        var isCompleted = task.IsCompleted;

        // Assert
        isCompleted.Should().BeFalse();
    }

    [Fact]
    public void HaveUtcCreatedAtSet()
    {
        // Arrange
        var task = new UserSession();

        // Act
        var createdAt = task.CreatedAt;

        // Assert
        createdAt.Should().NotBe(default);
        createdAt.Kind.Should().Be(DateTimeKind.Utc);
    }

}

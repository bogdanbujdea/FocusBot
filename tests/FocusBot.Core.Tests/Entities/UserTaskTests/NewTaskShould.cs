using FocusBot.Core.Entities;
using TaskStatus = FocusBot.Core.Entities.TaskStatus;

namespace FocusBot.Core.Tests.Entities.UserTaskTests;

public class NewTaskShould
{
    [Fact]
    public void HaveNonEmptyTaskId()
    {
        // Arrange
        var task = new UserTask();

        // Act
        var taskId = task.TaskId;

        // Assert
        taskId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void HaveParseableGuidTaskId()
    {
        // Arrange
        var task = new UserTask();

        // Act
        var parseable = Guid.TryParse(task.TaskId, out _);

        // Assert
        parseable.Should().BeTrue();
    }

    [Fact]
    public void DefaultStatusToToDo()
    {
        // Arrange
        var task = new UserTask();

        // Act
        var status = task.Status;

        // Assert
        status.Should().Be(TaskStatus.ToDo);
    }

    [Fact]
    public void HaveUtcCreatedAtSet()
    {
        // Arrange
        var task = new UserTask();

        // Act
        var createdAt = task.CreatedAt;

        // Assert
        createdAt.Should().NotBe(default);
        createdAt.Kind.Should().Be(DateTimeKind.Utc);
    }

}

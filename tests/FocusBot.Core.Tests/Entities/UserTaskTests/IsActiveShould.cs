using FocusBot.Core.Entities;
using TaskStatus = FocusBot.Core.Entities.TaskStatus;

namespace FocusBot.Core.Tests.Entities.UserTaskTests;

public class IsActiveShould
{
    [Fact]
    public void ReturnTrue_WhenStatusIsInProgress()
    {
        // Arrange
        var task = new UserTask { Status = TaskStatus.InProgress };

        // Act
        var isActive = task.IsActive;

        // Assert
        isActive.Should().BeTrue();
    }

    [Fact]
    public void ReturnFalse_WhenStatusIsToDo()
    {
        // Arrange
        var task = new UserTask { Status = TaskStatus.ToDo };

        // Act
        var isActive = task.IsActive;

        // Assert
        isActive.Should().BeFalse();
    }

    [Fact]
    public void ReturnFalse_WhenStatusIsDone()
    {
        // Arrange
        var task = new UserTask { Status = TaskStatus.Done };

        // Act
        var isActive = task.IsActive;

        // Assert
        isActive.Should().BeFalse();
    }
}

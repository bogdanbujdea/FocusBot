using FocusBot.Core.Entities;

namespace FocusBot.Core.Tests.Entities.UserTaskTests;

public class IsActiveShould
{
    [Fact]
    public void ReturnTrue_WhenNotCompleted()
    {
        // Arrange
        var task = new UserTask { IsCompleted = false };

        // Act
        var isActive = task.IsActive;

        // Assert
        isActive.Should().BeTrue();
    }

    [Fact]
    public void ReturnFalse_WhenCompleted()
    {
        // Arrange
        var task = new UserTask { IsCompleted = true };

        // Act
        var isActive = task.IsActive;

        // Assert
        isActive.Should().BeFalse();
    }
}

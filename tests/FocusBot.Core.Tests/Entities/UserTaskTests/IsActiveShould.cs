using FocusBot.Core.Entities;
using TaskStatus = FocusBot.Core.Entities.TaskStatus;

namespace FocusBot.Core.Tests.Entities.UserTaskTests;

public class IsActiveShould
{
    [Fact]
    public void ReturnTrue_WhenStatusIsInProgress()
    {
        var task = new UserTask { Status = TaskStatus.InProgress };
        task.IsActive.Should().BeTrue();
    }

    [Fact]
    public void ReturnFalse_WhenStatusIsToDo()
    {
        var task = new UserTask { Status = TaskStatus.ToDo };
        task.IsActive.Should().BeFalse();
    }

    [Fact]
    public void ReturnFalse_WhenStatusIsDone()
    {
        var task = new UserTask { Status = TaskStatus.Done };
        task.IsActive.Should().BeFalse();
    }
}

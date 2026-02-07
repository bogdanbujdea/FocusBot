using FocusBot.Core.Entities;
using TaskStatus = FocusBot.Core.Entities.TaskStatus;

namespace FocusBot.Core.Tests.Entities.UserTaskTests;

public class NewTaskShould
{
    [Fact]
    public void HaveNonEmptyTaskId()
    {
        var task = new UserTask();
        task.TaskId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void HaveParseableGuidTaskId()
    {
        var task = new UserTask();
        Guid.TryParse(task.TaskId, out _).Should().BeTrue();
    }

    [Fact]
    public void DefaultStatusToToDo()
    {
        var task = new UserTask();
        task.Status.Should().Be(TaskStatus.ToDo);
    }

    [Fact]
    public void HaveUtcCreatedAtSet()
    {
        var task = new UserTask();
        task.CreatedAt.Should().NotBe(default);
        task.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

}

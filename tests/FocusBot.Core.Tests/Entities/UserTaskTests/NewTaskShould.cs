using FocusBot.Core.Entities;

namespace FocusBot.Core.Tests.Entities.UserTaskTests;

public class NewTaskShould
{
    [Fact]
    public void HaveNonEmptyTaskId()
    {
        var task = UserSession.FromApiResponse(
            new ApiSessionResponse(
                Guid.NewGuid(),
                "Title",
                null,
                null,
                DateTime.UtcNow,
                null));

        task.SessionId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void HaveParseableGuidTaskId()
    {
        var task = UserSession.FromApiResponse(
            new ApiSessionResponse(
                Guid.NewGuid(),
                "Title",
                null,
                null,
                DateTime.UtcNow,
                null));

        var parseable = Guid.TryParse(task.SessionId, out _);

        parseable.Should().BeTrue();
    }

    [Fact]
    public void DefaultIsCompletedToFalse()
    {
        var task = UserSession.FromApiResponse(
            new ApiSessionResponse(
                Guid.NewGuid(),
                "Title",
                null,
                null,
                DateTime.UtcNow,
                null));

        task.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void HaveUtcCreatedAtSet()
    {
        var started = DateTime.UtcNow.AddMinutes(-5);
        var task = UserSession.FromApiResponse(
            new ApiSessionResponse(
                Guid.NewGuid(),
                "Title",
                null,
                null,
                started,
                null));

        task.CreatedAt.Should().Be(started);
        task.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }
}

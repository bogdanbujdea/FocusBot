using FocusBot.Core.Entities;

namespace FocusBot.Core.Tests.Entities.UserTaskTests;

public class IsActiveShould
{
    [Fact]
    public void ReturnTrue_WhenNotCompleted()
    {
        var task = UserSession.FromApiResponse(
            new ApiSessionResponse(
                Guid.NewGuid(),
                "Title",
                null,
                null,
                DateTime.UtcNow,
                null));

        task.IsActive.Should().BeTrue();
    }

    [Fact]
    public void ReturnFalse_WhenCompleted()
    {
        var task = new UserSession
        {
            SessionId = Guid.NewGuid().ToString(),
            SessionTitle = "T",
            IsCompleted = true,
        };

        task.IsActive.Should().BeFalse();
    }
}

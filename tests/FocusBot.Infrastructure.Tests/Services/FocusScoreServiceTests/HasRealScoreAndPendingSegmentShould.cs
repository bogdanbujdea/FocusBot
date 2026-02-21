namespace FocusBot.Infrastructure.Tests.Services.FocusScoreServiceTests;

public class HasRealScoreAndPendingSegmentShould : FocusScoreServiceTestBase
{
    [Fact]
    public void ReturnFalse_BeforeAnySegment()
    {
        Service.HasRealScore.Should().BeFalse();
    }

    [Fact]
    public void ReturnFalse_WhilePending()
    {
        Service.StartPendingSegment("task-1", "hash1", "Title", "Process");
        Service.HasRealScore.Should().BeFalse();
    }

    [Fact]
    public void ReturnTrue_AfterUpdatePendingSegmentScore_WithAnyScore()
    {
        Service.StartPendingSegment("task-1", "hash1", "Title", "Process");
        Service.UpdatePendingSegmentScore(8);
        Service.HasRealScore.Should().BeTrue();
    }

    [Fact]
    public void ReturnTrue_WhenScoreIs5_BecauseItIsValidScore()
    {
        Service.StartPendingSegment("task-1", "hash1", "Title", "Process");
        Service.UpdatePendingSegmentScore(5);
        Service.HasRealScore.Should().BeTrue();
    }

    [Fact]
    public void ReturnTrue_AfterLoadingExistingSegments()
    {
        Service.StartOrResumeSegment("task-1", "hash1", 7, "Title", "Process");
        Service.PauseCurrentSegment();
        Service.ClearTaskSegments("task-1");
        Service.HasRealScore.Should().BeFalse();
    }
}

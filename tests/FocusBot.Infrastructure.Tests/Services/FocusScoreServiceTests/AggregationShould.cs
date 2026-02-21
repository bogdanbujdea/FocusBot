namespace FocusBot.Infrastructure.Tests.Services.FocusScoreServiceTests;

public class AggregationShould : FocusScoreServiceTestBase
{
    [Fact]
    public void AccumulateDuration_WhenSameContextResumedMultipleTimes()
    {
        Service.StartOrResumeSegment("task-1", "hash1", 9, "VS Code", "Code");
        Thread.Sleep(1100);
        Service.PauseCurrentSegment();
        Service.StartOrResumeSegment("task-1", "hash1", 9, "VS Code", "Code");
        Thread.Sleep(1100);
        Service.PauseCurrentSegment();
        Service.CalculateFocusScorePercent("task-1").Should().Be(90);
    }

    [Fact]
    public void CreateSeparateEntries_WhenSameContextWithDifferentScore()
    {
        Service.StartOrResumeSegment("task-1", "hash1", 9, "A", "App");
        Thread.Sleep(1100);
        Service.PauseCurrentSegment();
        Service.StartOrResumeSegment("task-1", "hash1", 3, "A", "App");
        Thread.Sleep(1100);
        Service.PauseCurrentSegment();
        var percent = Service.CalculateFocusScorePercent("task-1");
        percent.Should().BeInRange(30, 90);
    }

    [Fact]
    public void CreateSeparateEntries_WhenDifferentContexts()
    {
        Service.StartOrResumeSegment("task-1", "hash1", 8, "A", "App1");
        Thread.Sleep(1100);
        Service.PauseCurrentSegment();
        Service.StartOrResumeSegment("task-1", "hash2", 4, "B", "App2");
        Thread.Sleep(1100);
        Service.PauseCurrentSegment();
        var percent = Service.CalculateFocusScorePercent("task-1");
        percent.Should().Be(60);
    }
}

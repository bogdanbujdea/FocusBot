using FocusBot.Core.Entities;

namespace FocusBot.Infrastructure.Tests.Services.FocusScoreServiceTests;

public class CalculateFocusScorePercentShould : FocusScoreServiceTestBase
{
    [Fact]
    public void Return0_WhenNoSegments()
    {
        Service.CalculateFocusScorePercent("task-1").Should().Be(0);
    }

    [Fact]
    public async Task Return80_WhenSingleSegmentScore8For60Seconds()
    {
        await SeedSegmentsAsync("task-1", new[] { (8, 60) });
        Service.CalculateFocusScorePercent("task-1").Should().Be(80);
    }

    [Fact]
    public async Task WeightMultipleSegmentsCorrectly()
    {
        await SeedSegmentsAsync("task-1", new[] { (10, 50), (2, 50) });
        var totalSeconds = 100;
        var weightedSum = 10 * 50 + 2 * 50;
        var expected = (int)Math.Round((double)weightedSum / totalSeconds * 10);
        Service.CalculateFocusScorePercent("task-1").Should().Be(expected);
    }

    [Fact]
    public void Return0_WhenTotalTimeIsZero()
    {
        Service.StartOrResumeSegment("task-1", "hash1", 5, "A", "App1");
        Service.PauseCurrentSegment();
        Service.CalculateFocusScorePercent("task-1").Should().Be(0);
    }

    private async Task SeedSegmentsAsync(string taskId, IEnumerable<(int Score, int DurationSeconds)> segments)
    {
        var list = segments.Select((s, i) => new FocusSegment
        {
            TaskId = taskId,
            ContextHash = $"hash{i}",
            AlignmentScore = s.Score,
            DurationSeconds = s.DurationSeconds,
            WindowTitle = "Title",
            ProcessName = "Process",
        }).ToList();
        await Context.FocusSegments.AddRangeAsync(list);
        await Context.SaveChangesAsync();
        await Service.LoadSegmentsForTaskAsync(taskId);
    }
}

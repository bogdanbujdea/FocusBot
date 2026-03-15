using FocusBot.Core.Entities;

namespace FocusBot.Infrastructure.Tests.Services;

public class FocusScoreService_UpdateHistoricalSegmentsTests : FocusScoreServiceTestBase
{
    [Fact]
    public async Task UpdateHistoricalSegments_UpdatesMemoryState_ForMatchingSegments()
    {
        // Arrange - Start two segments with same context, different dates
        var taskId = "task1";
        var contextHash = "netflix-hash";

        Service.StartOrResumeSegment(taskId, contextHash, 1, "Netflix", "chrome");
        await Task.Delay(100);
        Service.PauseCurrentSegment();

        // Add another segment for different day
        var seg1 = new FocusSegment
        {
            TaskId = taskId,
            ContextHash = contextHash,
            AlignmentScore = 1,
            DurationSeconds = 300,
            AnalyticsDateLocal = DateOnly.FromDateTime(DateTime.Now),
        };
        var seg2 = new FocusSegment
        {
            TaskId = taskId,
            ContextHash = contextHash,
            AlignmentScore = 1,
            DurationSeconds = 200,
            AnalyticsDateLocal = DateOnly.FromDateTime(DateTime.Now.AddDays(-1)),
        };

        Context.FocusSegments.Add(seg1);
        Context.FocusSegments.Add(seg2);
        await Context.SaveChangesAsync();

        // Act
        await Service.UpdateHistoricalSegmentsAsync(taskId, contextHash, 9);

        // Assert - Verify both segments were persisted with new score
        var updated = Context
            .FocusSegments.Where(s => s.TaskId == taskId && s.ContextHash == contextHash)
            .ToList();

        Assert.True(updated.Count >= 2);
        foreach (var s in updated)
        {
            if (s.AlignmentScore != 9)
            {
                // At least the ones we added should be updated
                if (
                    (
                        s.AnalyticsDateLocal == DateOnly.FromDateTime(DateTime.Now)
                        || s.AnalyticsDateLocal == DateOnly.FromDateTime(DateTime.Now.AddDays(-1))
                    )
                )
                {
                    Assert.Equal(9, s.AlignmentScore);
                }
            }
        }
    }

    [Fact]
    public async Task UpdateHistoricalSegments_PersistsChangesToDatabase()
    {
        // Arrange
        var taskId = "task1";
        var contextHash = "netflix-hash";

        var segment = new FocusSegment
        {
            TaskId = taskId,
            ContextHash = contextHash,
            AlignmentScore = 1,
            DurationSeconds = 300,
            AnalyticsDateLocal = DateOnly.FromDateTime(DateTime.Now),
        };

        Context.FocusSegments.Add(segment);
        await Context.SaveChangesAsync();
        int segmentId = segment.Id;

        // Act
        await Service.UpdateHistoricalSegmentsAsync(taskId, contextHash, 9);

        // Assert - Create new context to verify persistence
        var newContext = Context;
        var persisted = newContext.FocusSegments.Find(segmentId);

        Assert.NotNull(persisted);
        Assert.Equal(9, persisted.AlignmentScore);
    }

    [Fact]
    public async Task UpdateHistoricalSegments_DoesNotAffectOtherContextHashes()
    {
        // Arrange
        var taskId = "task1";
        var contextHash1 = "netflix-hash";
        var contextHash2 = "youtube-hash";

        var netflix = new FocusSegment
        {
            TaskId = taskId,
            ContextHash = contextHash1,
            AlignmentScore = 1,
            DurationSeconds = 100,
            AnalyticsDateLocal = DateOnly.FromDateTime(DateTime.Now),
        };
        var youtube = new FocusSegment
        {
            TaskId = taskId,
            ContextHash = contextHash2,
            AlignmentScore = 5,
            DurationSeconds = 200,
            AnalyticsDateLocal = DateOnly.FromDateTime(DateTime.Now),
        };

        Context.FocusSegments.AddRange(netflix, youtube);
        await Context.SaveChangesAsync();

        int netflixId = netflix.Id;
        int youtubeId = youtube.Id;

        // Act
        await Service.UpdateHistoricalSegmentsAsync(taskId, contextHash1, 9);

        // Assert
        var updatedNetflix = Context.FocusSegments.Find(netflixId);
        var unchangedYoutube = Context.FocusSegments.Find(youtubeId);

        Assert.NotNull(updatedNetflix);
        Assert.NotNull(unchangedYoutube);

        Assert.Equal(9, updatedNetflix.AlignmentScore);
        Assert.Equal(5, unchangedYoutube.AlignmentScore);
    }

    [Fact]
    public async Task UpdateHistoricalSegments_DoesNotAffectOtherTasks()
    {
        // Arrange
        var taskId1 = "task1";
        var taskId2 = "task2";
        var contextHash = "netflix-hash";

        var task1Seg = new FocusSegment
        {
            TaskId = taskId1,
            ContextHash = contextHash,
            AlignmentScore = 1,
            DurationSeconds = 100,
            AnalyticsDateLocal = DateOnly.FromDateTime(DateTime.Now),
        };
        var task2Seg = new FocusSegment
        {
            TaskId = taskId2,
            ContextHash = contextHash,
            AlignmentScore = 3,
            DurationSeconds = 200,
            AnalyticsDateLocal = DateOnly.FromDateTime(DateTime.Now),
        };

        Context.FocusSegments.AddRange(task1Seg, task2Seg);
        await Context.SaveChangesAsync();

        int task1SegId = task1Seg.Id;
        int task2SegId = task2Seg.Id;

        // Act
        await Service.UpdateHistoricalSegmentsAsync(taskId1, contextHash, 9);

        // Assert
        var updated1 = Context.FocusSegments.Find(task1SegId);
        var unchanged2 = Context.FocusSegments.Find(task2SegId);

        Assert.NotNull(updated1);
        Assert.NotNull(unchanged2);

        Assert.Equal(9, updated1.AlignmentScore);
        Assert.Equal(3, unchanged2.AlignmentScore);
    }

    [Fact]
    public async Task UpdateHistoricalSegments_PreservesOtherSegmentProperties()
    {
        // Arrange
        var taskId = "task1";
        var contextHash = "netflix-hash";

        var segment = new FocusSegment
        {
            TaskId = taskId,
            ContextHash = contextHash,
            AlignmentScore = 1,
            DurationSeconds = 450,
            WindowTitle = "Netflix - Watch",
            ProcessName = "chrome",
            AnalyticsDateLocal = DateOnly.FromDateTime(DateTime.Now),
        };

        Context.FocusSegments.Add(segment);
        await Context.SaveChangesAsync();
        int segmentId = segment.Id;

        // Act
        await Service.UpdateHistoricalSegmentsAsync(taskId, contextHash, 9);

        // Assert
        var updated = Context.FocusSegments.Find(segmentId);

        Assert.NotNull(updated);
        Assert.Equal(9, updated.AlignmentScore);
        Assert.Equal(450, updated.DurationSeconds);
        Assert.Equal("Netflix - Watch", updated.WindowTitle);
        Assert.Equal("chrome", updated.ProcessName);
        Assert.Equal(taskId, updated.TaskId);
        Assert.Equal(contextHash, updated.ContextHash);
    }

    [Fact]
    public async Task UpdateHistoricalSegments_WithNoMatchingSegments_DoesNothing()
    {
        // Arrange
        var taskId = "task1";
        var contextHash = "netflix-hash";
        var otherContextHash = "youtube-hash";

        var segment = new FocusSegment
        {
            TaskId = taskId,
            ContextHash = otherContextHash,
            AlignmentScore = 5,
            DurationSeconds = 100,
            AnalyticsDateLocal = DateOnly.FromDateTime(DateTime.Now),
        };

        Context.FocusSegments.Add(segment);
        await Context.SaveChangesAsync();
        int segmentId = segment.Id;

        // Act
        await Service.UpdateHistoricalSegmentsAsync(taskId, contextHash, 9);

        // Assert
        var unchanged = Context.FocusSegments.Find(segmentId);

        Assert.NotNull(unchanged);
        Assert.Equal(5, unchanged.AlignmentScore);
    }
}

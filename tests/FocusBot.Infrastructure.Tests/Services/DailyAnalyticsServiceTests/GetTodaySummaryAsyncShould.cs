using FocusBot.Core.Events;
using FocusBot.Infrastructure.Data;
using FocusBot.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace FocusBot.Infrastructure.Tests.Services.DailyAnalyticsServiceTests;

public class GetTodaySummaryAsyncShould
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task SumFocusedSeconds_FromSegmentsWithHighScore()
    {
        // Arrange
        using var context = CreateContext();
        var service = new DailyAnalyticsService(context);
        var today = DateOnly.FromDateTime(DateTime.Now);

        var segment = new Core.Entities.FocusSegment
        {
            TaskId = "task-1",
            ContextHash = "hash-1",
            AlignmentScore = 7,
            DurationSeconds = 300,
            AnalyticsDateLocal = today,
        };
        context.FocusSegments.Add(segment);
        await context.SaveChangesAsync();

        // Act
        var summary = await service.GetTodaySummaryAsync(DateTime.Now);

        // Assert
        summary.Should().NotBeNull();
        summary!.FocusedTime.Should().Be(TimeSpan.FromSeconds(300));
    }

    [Fact]
    public async Task SumDistractedSeconds_FromSegmentsWithLowScore()
    {
        // Arrange
        using var context = CreateContext();
        var service = new DailyAnalyticsService(context);
        var today = DateOnly.FromDateTime(DateTime.Now);

        var segment = new Core.Entities.FocusSegment
        {
            TaskId = "task-1",
            ContextHash = "hash-1",
            AlignmentScore = 2,
            DurationSeconds = 100,
            AnalyticsDateLocal = today,
        };
        context.FocusSegments.Add(segment);
        await context.SaveChangesAsync();

        // Act
        var summary = await service.GetTodaySummaryAsync(DateTime.Now);

        // Assert
        summary.Should().NotBeNull();
        summary!.DistractedTime.Should().Be(TimeSpan.FromSeconds(100));
    }

    [Fact]
    public async Task ExcludeDeletedTaskSegments_AfterTaskDeleted()
    {
        // Arrange
        using var context = CreateContext();
        var service = new DailyAnalyticsService(context);
        var today = DateOnly.FromDateTime(DateTime.Now);

        var segment1 = new Core.Entities.FocusSegment
        {
            TaskId = "task-1",
            ContextHash = "hash-1",
            AlignmentScore = 7,
            DurationSeconds = 300,
            AnalyticsDateLocal = today,
        };
        var segment2 = new Core.Entities.FocusSegment
        {
            TaskId = "task-2",
            ContextHash = "hash-1",
            AlignmentScore = 7,
            DurationSeconds = 200,
            AnalyticsDateLocal = today,
        };
        context.FocusSegments.Add(segment1);
        context.FocusSegments.Add(segment2);
        await context.SaveChangesAsync();

        // Act - Delete task-1 segments and reload
        context.FocusSegments.RemoveRange(context.FocusSegments.Where(s => s.TaskId == "task-1"));
        await context.SaveChangesAsync();
        await service.ReloadTodayFromDbAsync();

        var summary = await service.GetTodaySummaryAsync(DateTime.Now);

        // Assert
        summary.Should().NotBeNull();
        summary!.FocusedTime.Should().Be(TimeSpan.FromSeconds(200));
    }

    [Fact]
    public async Task ReturnNull_WhenNoSegmentsForToday()
    {
        // Arrange
        using var context = CreateContext();
        var service = new DailyAnalyticsService(context);

        // Act
        var summary = await service.GetTodaySummaryAsync(DateTime.Now);

        // Assert
        summary.Should().BeNull();
    }

    [Fact]
    public async Task CalculateAverageDistractionDuration_AsDistractedSecondsDividedByDistractionCount()
    {
        // Arrange
        using var context = CreateContext();
        var service = new DailyAnalyticsService(context);
        var today = DateOnly.FromDateTime(DateTime.Now);

        // Create a segment with 39 seconds of distracted time (AlignmentScore < 4)
        var segment = new Core.Entities.FocusSegment
        {
            TaskId = "task-1",
            ContextHash = "hash-1",
            AlignmentScore = 2,
            DurationSeconds = 39,
            AnalyticsDateLocal = today,
        };
        context.FocusSegments.Add(segment);
        await context.SaveChangesAsync();

        // Register 1 distraction event
        var distractionEvent = new Core.Entities.DistractionEvent
        {
            OccurredAtUtc = DateTime.UtcNow,
            TaskId = "task-1",
            ProcessName = "chrome.exe",
            WindowTitleSnapshot = "Some Website",
            DistractedDurationSecondsAtEmit = 5,
        };
        context.DistractionEvents.Add(distractionEvent);
        await context.SaveChangesAsync();

        await service.ReloadTodayFromDbAsync();

        // Act
        var summary = await service.GetTodaySummaryAsync(DateTime.Now);

        // Assert
        summary.Should().NotBeNull();
        summary!.DistractedTime.Should().Be(TimeSpan.FromSeconds(39));
        summary!.DistractionCount.Should().Be(1);
        summary!.AverageDistractionDuration.Should().Be(TimeSpan.FromSeconds(39));
    }
}

using FocusBot.Core.Events;
using FocusBot.Infrastructure.Data;
using FocusBot.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace FocusBot.Infrastructure.Tests.Services.DailyAnalyticsServiceTests;

public class DailyAnalyticsServiceShould
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
    public async Task IncrementFocusedSeconds_WhenTickIsFocusedForToday()
    {
        // Arrange
        using var context = CreateContext();
        var service = new DailyAnalyticsService(context);
        var nowUtc = DateTime.UtcNow;

        // Act
        await service.UpdateForTickAsync(nowUtc, FocusStatus.Focused);

        // Assert
        var localDate = DateOnly.FromDateTime(nowUtc.ToLocalTime());
        var entity = context.DailyFocusAnalytics.Single(x => x.AnalyticsDateLocal == localDate);
        entity.FocusedSeconds.Should().Be(1);
        entity.TotalTrackedSeconds.Should().Be(1);
    }

    [Fact]
    public async Task IncrementDistractedSeconds_WhenTickIsDistractedForToday()
    {
        // Arrange
        using var context = CreateContext();
        var service = new DailyAnalyticsService(context);
        var nowUtc = DateTime.UtcNow;

        // Act
        await service.UpdateForTickAsync(nowUtc, FocusStatus.Distracted);

        // Assert
        var localDate = DateOnly.FromDateTime(nowUtc.ToLocalTime());
        var entity = context.DailyFocusAnalytics.Single(x => x.AnalyticsDateLocal == localDate);
        entity.DistractedSeconds.Should().Be(1);
        entity.TotalTrackedSeconds.Should().Be(1);
    }

    [Fact]
    public async Task IncrementDistractionCount_WhenDistractionEventRegisteredForToday()
    {
        // Arrange
        using var context = CreateContext();
        var service = new DailyAnalyticsService(context);
        var nowUtc = DateTime.UtcNow;
        var distractionEvent = new Core.Entities.DistractionEvent
        {
            TaskId = "task-1",
            OccurredAtUtc = nowUtc,
            ProcessName = "App1",
            DistractedDurationSecondsAtEmit = 10
        };

        // Act
        await service.RegisterDistractionEventAsync(distractionEvent);

        // Assert
        var localDate = DateOnly.FromDateTime(nowUtc.ToLocalTime());
        var entity = context.DailyFocusAnalytics.Single(x => x.AnalyticsDateLocal == localDate);
        entity.DistractionCount.Should().Be(1);
        entity.DistractedSeconds.Should().Be(10);
    }

    [Fact]
    public async Task MapToFocusScoreBucket_WhenSummaryRequested()
    {
        // Arrange
        using var context = CreateContext();
        var service = new DailyAnalyticsService(context);
        var nowUtc = DateTime.UtcNow;

        for (var i = 0; i < 6; i++)
            await service.UpdateForTickAsync(nowUtc, FocusStatus.Focused);

        for (var i = 0; i < 4; i++)
            await service.UpdateForTickAsync(nowUtc, FocusStatus.Distracted);

        // Act
        var summary = await service.GetTodaySummaryAsync(DateTime.Now);

        // Assert
        summary.Should().NotBeNull();
        summary!.FocusScoreBucket.Should().BeInRange(1, 10);
    }

    [Fact]
    public async Task ReturnNull_WhenNoDataExistsForToday()
    {
        // Arrange
        using var context = CreateContext();
        var service = new DailyAnalyticsService(context);

        // Act
        var summary = await service.GetTodaySummaryAsync(DateTime.Now);

        // Assert
        summary.Should().BeNull();
    }
}


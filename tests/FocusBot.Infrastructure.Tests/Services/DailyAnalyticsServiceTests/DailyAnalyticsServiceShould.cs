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
        
        // Assert - Verify the accumulator was updated (reflected in GetTodaySummaryAsync)
        var summary = await service.GetTodaySummaryAsync(DateTime.Now);
        summary.Should().NotBeNull();
        summary!.FocusedTime.Should().Be(TimeSpan.FromSeconds(1));
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

        // Assert - Verify the accumulator was updated
        var summary = await service.GetTodaySummaryAsync(DateTime.Now);
        summary.Should().NotBeNull();
        summary!.DistractedTime.Should().Be(TimeSpan.FromSeconds(1));
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
        summary!.FocusScoreBucket.Should().BeInRange(0, 10);
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

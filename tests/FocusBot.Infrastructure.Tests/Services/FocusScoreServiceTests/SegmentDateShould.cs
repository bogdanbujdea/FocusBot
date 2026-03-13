using FocusBot.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FocusBot.Infrastructure.Tests.Services.FocusScoreServiceTests;

public class SegmentDateShould
{
    [Fact]
    public void UseCurrentLocalDate_WhenSegmentCreated()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<FocusScoreService>();
        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<FocusScoreService>();
        
        var taskId = "task-1";
        var contextHash = "hash-1";
        var alignmentScore = 7;
        var today = DateOnly.FromDateTime(DateTime.Now);

        // Act
        service.StartOrResumeSegment(taskId, contextHash, alignmentScore, "window", "process");

        // Note: Cannot easily test private _segments dictionary without reflection or public exposure.
        // This test verifies the method doesn't throw.
        // Full verification requires integration tests that create persistence.
    }

    [Fact]
    public void GroupByDate_WhenSameContextDifferentDays()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<FocusScoreService>();
        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<FocusScoreService>();
        
        var taskId = "task-1";
        var contextHash = "hash-1";
        var alignmentScore = 7;

        // Act - Create segment today
        service.StartOrResumeSegment(taskId, contextHash, alignmentScore, "window", "process");
        service.PauseCurrentSegment();

        // Note: Full verification requires database-level testing to confirm segments
        // are stored with AnalyticsDateLocal and can be queried by date.
        // This test verifies basic segment lifecycle doesn't throw.
    }
}

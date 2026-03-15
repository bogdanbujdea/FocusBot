using FocusBot.Infrastructure.Data;
using FocusBot.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace FocusBot.Infrastructure.Tests.Services.SessionDistractionAnalyticsServiceTests;

public class SessionDistractionAnalyticsServiceShould
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
    public async Task ReturnZeroDistractions_WhenSessionHasNoEvents()
    {
        // Arrange
        using var context = CreateContext();
        var service = new SessionDistractionAnalyticsService(context);
        var sessionId = Guid.NewGuid();

        // Act
        var summary = await service.GetSessionSummaryAsync(sessionId);

        // Assert
        summary.TotalDistractionCount.Should().Be(0);
        summary.TopApps.Should().BeEmpty();
    }

    [Fact]
    public async Task ReturnSessionDistractionCount_WhenSessionHasEvents()
    {
        // Arrange
        using var context = CreateContext();
        var sessionId = Guid.NewGuid();
        context.DistractionEvents.Add(
            new Core.Entities.DistractionEvent
            {
                TaskId = "task-1",
                SessionId = sessionId,
                ProcessName = "App1",
                OccurredAtUtc = DateTime.UtcNow,
                DistractedDurationSecondsAtEmit = 6,
            }
        );
        context.DistractionEvents.Add(
            new Core.Entities.DistractionEvent
            {
                TaskId = "task-1",
                SessionId = sessionId,
                ProcessName = "App2",
                OccurredAtUtc = DateTime.UtcNow.AddSeconds(10),
                DistractedDurationSecondsAtEmit = 7,
            }
        );
        await context.SaveChangesAsync();
        var service = new SessionDistractionAnalyticsService(context);

        // Act
        var summary = await service.GetSessionSummaryAsync(sessionId);

        // Assert
        summary.TotalDistractionCount.Should().Be(2);
    }

    [Fact]
    public async Task IgnoreEventsOutsideSession_WhenCalculatingSummary()
    {
        // Arrange
        using var context = CreateContext();
        var sessionId = Guid.NewGuid();
        var otherSessionId = Guid.NewGuid();

        context.DistractionEvents.Add(
            new Core.Entities.DistractionEvent
            {
                TaskId = "task-1",
                SessionId = sessionId,
                ProcessName = "App1",
                OccurredAtUtc = DateTime.UtcNow,
                DistractedDurationSecondsAtEmit = 6,
            }
        );
        context.DistractionEvents.Add(
            new Core.Entities.DistractionEvent
            {
                TaskId = "task-1",
                SessionId = otherSessionId,
                ProcessName = "App1",
                OccurredAtUtc = DateTime.UtcNow.AddSeconds(10),
                DistractedDurationSecondsAtEmit = 8,
            }
        );
        await context.SaveChangesAsync();
        var service = new SessionDistractionAnalyticsService(context);

        // Act
        var summary = await service.GetSessionSummaryAsync(sessionId);

        // Assert
        summary.TotalDistractionCount.Should().Be(1);
        summary.TopApps.Should().HaveCount(1);
    }

    [Fact]
    public async Task ReturnTopAppsOrderedByDurationThenCount()
    {
        // Arrange
        using var context = CreateContext();
        var sessionId = Guid.NewGuid();

        // AppA: total duration 20 (2 events)
        context.DistractionEvents.Add(
            new Core.Entities.DistractionEvent
            {
                TaskId = "task-1",
                SessionId = sessionId,
                ProcessName = "AppA",
                OccurredAtUtc = DateTime.UtcNow,
                DistractedDurationSecondsAtEmit = 10,
            }
        );
        context.DistractionEvents.Add(
            new Core.Entities.DistractionEvent
            {
                TaskId = "task-1",
                SessionId = sessionId,
                ProcessName = "AppA",
                OccurredAtUtc = DateTime.UtcNow.AddSeconds(10),
                DistractedDurationSecondsAtEmit = 10,
            }
        );

        // AppB: total duration 15 (3 events)
        for (var i = 0; i < 3; i++)
        {
            context.DistractionEvents.Add(
                new Core.Entities.DistractionEvent
                {
                    TaskId = "task-1",
                    SessionId = sessionId,
                    ProcessName = "AppB",
                    OccurredAtUtc = DateTime.UtcNow.AddSeconds(20 + i),
                    DistractedDurationSecondsAtEmit = 5,
                }
            );
        }

        // AppC: duration 20 but fewer events than AppA to exercise tie-breakers
        context.DistractionEvents.Add(
            new Core.Entities.DistractionEvent
            {
                TaskId = "task-1",
                SessionId = sessionId,
                ProcessName = "AppC",
                OccurredAtUtc = DateTime.UtcNow.AddSeconds(40),
                DistractedDurationSecondsAtEmit = 20,
            }
        );

        await context.SaveChangesAsync();
        var service = new SessionDistractionAnalyticsService(context);

        // Act
        var summary = await service.GetSessionSummaryAsync(sessionId);

        // Assert
        summary.TopApps.Should().HaveCount(3);
        summary.TopApps[0].DistractedDurationSeconds.Should().Be(20);
    }
}

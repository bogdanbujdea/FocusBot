using FocusBot.Core.Entities;
using FocusBot.Core.Events;
using FocusBot.Core.Interfaces;
using FocusBot.Infrastructure.Services;

namespace FocusBot.Infrastructure.Tests.Services.DistractionDetectorServiceTests;

public class DistractionDetectorServiceShould
{
    [Fact]
    public async Task EmitSingleEvent_WhenDistractedStatePersistsForFiveSeconds()
    {
        // Arrange
        var repo = new InMemoryDistractionEventRepository();
        var service = new DistractionDetectorService(repo);
        var start = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        await service.OnSampleAsync("task-1", FocusStatus.Neutral, "App", "Window", start);
        await service.OnSampleAsync("task-1", FocusStatus.Distracted, "App", "Window", start.AddSeconds(0));
        await service.OnSampleAsync("task-1", FocusStatus.Distracted, "App", "Window", start.AddSeconds(4));
        await service.OnSampleAsync("task-1", FocusStatus.Distracted, "App", "Window", start.AddSeconds(5));

        // Assert
        repo.Events.Count.Should().Be(1);
        var ev = repo.Events.Single();
        ev.TaskId.Should().Be("task-1");
        ev.ProcessName.Should().Be("App");
        ev.WindowTitleSnapshot.Should().Be("Window");
        ev.DistractedDurationSecondsAtEmit.Should().Be(5);
    }

    [Fact]
    public async Task NotEmit_WhenDistractedStateEndsBeforeFiveSeconds()
    {
        // Arrange
        var repo = new InMemoryDistractionEventRepository();
        var service = new DistractionDetectorService(repo);
        var start = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        await service.OnSampleAsync("task-1", FocusStatus.Distracted, "App", "Window", start);
        await service.OnSampleAsync("task-1", FocusStatus.Distracted, "App", "Window", start.AddSeconds(3));
        await service.OnSampleAsync("task-1", FocusStatus.Focused, "App", "Window", start.AddSeconds(4));

        // Assert
        repo.Events.Count.Should().Be(0);
    }

    [Fact]
    public async Task NotEmitRepeatedly_WhenStillInSameDistractedEpisode()
    {
        // Arrange
        var repo = new InMemoryDistractionEventRepository();
        var service = new DistractionDetectorService(repo);
        var start = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        await service.OnSampleAsync("task-1", FocusStatus.Distracted, "App", "Window", start);
        await service.OnSampleAsync("task-1", FocusStatus.Distracted, "App", "Window", start.AddSeconds(5));
        await service.OnSampleAsync("task-1", FocusStatus.Distracted, "App", "Window", start.AddSeconds(10));

        // Assert
        repo.Events.Count.Should().Be(1);
    }

    [Fact]
    public async Task EmitSecondEvent_WhenStateReentersDistractedAfterExit()
    {
        // Arrange
        var repo = new InMemoryDistractionEventRepository();
        var service = new DistractionDetectorService(repo);
        var start = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        await service.OnSampleAsync("task-1", FocusStatus.Distracted, "App", "Window", start);
        await service.OnSampleAsync("task-1", FocusStatus.Distracted, "App", "Window", start.AddSeconds(5));
        await service.OnSampleAsync("task-1", FocusStatus.Focused, "App", "Window", start.AddSeconds(6));

        await service.OnSampleAsync("task-1", FocusStatus.Distracted, "App", "Window", start.AddSeconds(10));
        await service.OnSampleAsync("task-1", FocusStatus.Distracted, "App", "Window", start.AddSeconds(15));

        // Assert
        repo.Events.Count.Should().Be(2);
    }

    [Fact]
    public async Task ResetCandidate_WhenStateBecomesFocusedBeforeThreshold()
    {
        // Arrange
        var repo = new InMemoryDistractionEventRepository();
        var service = new DistractionDetectorService(repo);
        var start = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        await service.OnSampleAsync("task-1", FocusStatus.Distracted, "App", "Window", start);
        await service.OnSampleAsync("task-1", FocusStatus.Focused, "App", "Window", start.AddSeconds(2));
        await service.OnSampleAsync("task-1", FocusStatus.Distracted, "App", "Window", start.AddSeconds(3));
        await service.OnSampleAsync("task-1", FocusStatus.Distracted, "App", "Window", start.AddSeconds(8));

        // Assert
        repo.Events.Count.Should().Be(1);
        repo.Events.Single().DistractedDurationSecondsAtEmit.Should().Be(5);
    }

    private sealed class InMemoryDistractionEventRepository : IDistractionEventRepository
    {
        public List<DistractionEvent> Events { get; } = [];

        public Task AddAsync(DistractionEvent distractionEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(distractionEvent);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DistractionEvent>> GetEventsForTaskBetweenAsync(
            string taskId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<DistractionEvent> result = Events
                .Where(e => e.TaskId == taskId && e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc <= toUtc)
                .ToList();
            return Task.FromResult(result);
        }
    }
}


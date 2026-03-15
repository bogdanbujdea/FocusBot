using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using FocusBot.Infrastructure.Data;
using FocusBot.Infrastructure.Repositories;
using FocusBot.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace FocusBot.Infrastructure.Tests.Services.TaskSummaryServiceTests;

public class TaskSummaryServiceShould
{
    private static (AppDbContext Context, ITaskRepository TaskRepo, IDistractionEventRepository EventRepo, ITaskSummaryService Service) CreateSetup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        var taskRepo = new TaskRepository(context);
        var eventRepo = new DistractionEventRepository(context);
        var services = new ServiceCollection();
        services.AddSingleton(context);
        services.AddSingleton<ITaskRepository>(taskRepo);
        services.AddSingleton<IDistractionEventRepository>(eventRepo);
        services.AddSingleton<ITaskSummaryService, TaskSummaryService>();
        var service = services.BuildServiceProvider().GetRequiredService<ITaskSummaryService>();
        return (context, taskRepo, eventRepo, service);
    }

    [Fact]
    public async Task ComputeFocusedSeconds_FromHighScoreSegments()
    {
        var (context, taskRepo, _, service) = CreateSetup();
        using (context)
        {
            var task = await taskRepo.AddTaskAsync("Test", null);
            await taskRepo.SetActiveAsync(task.TaskId);

            context.FocusSegments.AddRange(
                new FocusSegment { TaskId = task.TaskId, ContextHash = "h1", AlignmentScore = 8, DurationSeconds = 100, AnalyticsDateLocal = DateOnly.FromDateTime(DateTime.UtcNow) },
                new FocusSegment { TaskId = task.TaskId, ContextHash = "h2", AlignmentScore = 6, DurationSeconds = 50, AnalyticsDateLocal = DateOnly.FromDateTime(DateTime.UtcNow) }
            );
            await context.SaveChangesAsync();

            await service.ComputeAndPersistSummaryAsync(task.TaskId);

            var updated = await taskRepo.GetByIdAsync(task.TaskId);
            updated.Should().NotBeNull();
            updated!.FocusedSeconds.Should().Be(150);
        }
    }

    [Fact]
    public async Task ComputeDistractedSeconds_FromLowScoreSegments()
    {
        var (context, taskRepo, _, service) = CreateSetup();
        using (context)
        {
            var task = await taskRepo.AddTaskAsync("Test", null);
            await taskRepo.SetActiveAsync(task.TaskId);

            context.FocusSegments.AddRange(
                new FocusSegment { TaskId = task.TaskId, ContextHash = "h1", AlignmentScore = 2, DurationSeconds = 60, AnalyticsDateLocal = DateOnly.FromDateTime(DateTime.UtcNow) },
                new FocusSegment { TaskId = task.TaskId, ContextHash = "h2", AlignmentScore = 3, DurationSeconds = 40, AnalyticsDateLocal = DateOnly.FromDateTime(DateTime.UtcNow) }
            );
            await context.SaveChangesAsync();

            await service.ComputeAndPersistSummaryAsync(task.TaskId);

            var updated = await taskRepo.GetByIdAsync(task.TaskId);
            updated.Should().NotBeNull();
            updated!.DistractedSeconds.Should().Be(100);
        }
    }

    [Fact]
    public async Task CountDistractionEvents_Accurately()
    {
        var (context, taskRepo, eventRepo, service) = CreateSetup();
        using (context)
        {
            var task = await taskRepo.AddTaskAsync("Test", null);
            await taskRepo.SetActiveAsync(task.TaskId);

            await eventRepo.AddAsync(new DistractionEvent
            {
                TaskId = task.TaskId,
                OccurredAtUtc = DateTime.UtcNow,
                ProcessName = "chrome",
                DistractedDurationSecondsAtEmit = 30
            });
            await eventRepo.AddAsync(new DistractionEvent
            {
                TaskId = task.TaskId,
                OccurredAtUtc = DateTime.UtcNow.AddSeconds(1),
                ProcessName = "slack",
                DistractedDurationSecondsAtEmit = 20
            });

            await service.ComputeAndPersistSummaryAsync(task.TaskId);

            var updated = await taskRepo.GetByIdAsync(task.TaskId);
            updated.Should().NotBeNull();
            updated!.DistractionCount.Should().Be(2);
        }
    }

    [Fact]
    public async Task ComputeContextSwitchCost_FromShortSegments()
    {
        var (context, taskRepo, _, service) = CreateSetup();
        using (context)
        {
            var task = await taskRepo.AddTaskAsync("Test", null);
            await taskRepo.SetActiveAsync(task.TaskId);

            context.FocusSegments.AddRange(
                new FocusSegment { TaskId = task.TaskId, ContextHash = "h1", AlignmentScore = 7, DurationSeconds = 60, AnalyticsDateLocal = DateOnly.FromDateTime(DateTime.UtcNow) },
                new FocusSegment { TaskId = task.TaskId, ContextHash = "h2", AlignmentScore = 5, DurationSeconds = 90, AnalyticsDateLocal = DateOnly.FromDateTime(DateTime.UtcNow) },
                new FocusSegment { TaskId = task.TaskId, ContextHash = "h3", AlignmentScore = 8, DurationSeconds = 200, AnalyticsDateLocal = DateOnly.FromDateTime(DateTime.UtcNow) }
            );
            await context.SaveChangesAsync();

            await service.ComputeAndPersistSummaryAsync(task.TaskId);

            var updated = await taskRepo.GetByIdAsync(task.TaskId);
            updated.Should().NotBeNull();
            updated!.ContextSwitchCostSeconds.Should().Be(150);
        }
    }

    [Fact]
    public async Task SerializeTopDistractingApps_OrderedBySeconds()
    {
        var (context, taskRepo, eventRepo, service) = CreateSetup();
        using (context)
        {
            var task = await taskRepo.AddTaskAsync("Test", null);
            await taskRepo.SetActiveAsync(task.TaskId);

            await eventRepo.AddAsync(new DistractionEvent { TaskId = task.TaskId, OccurredAtUtc = DateTime.UtcNow, ProcessName = "slack", DistractedDurationSecondsAtEmit = 50 });
            await eventRepo.AddAsync(new DistractionEvent { TaskId = task.TaskId, OccurredAtUtc = DateTime.UtcNow.AddSeconds(1), ProcessName = "chrome", DistractedDurationSecondsAtEmit = 100 });
            await eventRepo.AddAsync(new DistractionEvent { TaskId = task.TaskId, OccurredAtUtc = DateTime.UtcNow.AddSeconds(2), ProcessName = "chrome", DistractedDurationSecondsAtEmit = 30 });

            await service.ComputeAndPersistSummaryAsync(task.TaskId);

            var updated = await taskRepo.GetByIdAsync(task.TaskId);
            updated.Should().NotBeNull();
            updated!.TopDistractingApps.Should().NotBeNullOrEmpty();
            updated.TopDistractingApps.Should().Contain("chrome");
            updated.TopDistractingApps.Should().Contain("slack");
            updated.TopDistractingApps.Should().Contain("130");
            updated.TopDistractingApps.Should().Contain("50");
        }
    }

    [Fact]
    public async Task HandleEmptySegmentsAndEvents_Gracefully()
    {
        var (context, taskRepo, _, service) = CreateSetup();
        using (context)
        {
            var task = await taskRepo.AddTaskAsync("Test", null);
            await taskRepo.SetActiveAsync(task.TaskId);

            await service.ComputeAndPersistSummaryAsync(task.TaskId);

            var updated = await taskRepo.GetByIdAsync(task.TaskId);
            updated.Should().NotBeNull();
            updated!.FocusedSeconds.Should().Be(0);
            updated.DistractedSeconds.Should().Be(0);
            updated.DistractionCount.Should().Be(0);
            updated.ContextSwitchCostSeconds.Should().Be(0);
            updated.TopDistractingApps.Should().BeNull();
        }
    }

    [Fact]
    public async Task DeleteFocusSegments_AfterSummarization()
    {
        var (context, taskRepo, _, service) = CreateSetup();
        using (context)
        {
            var task = await taskRepo.AddTaskAsync("Test", null);
            await taskRepo.SetActiveAsync(task.TaskId);
            context.FocusSegments.Add(new FocusSegment { TaskId = task.TaskId, ContextHash = "h1", AlignmentScore = 7, DurationSeconds = 60, AnalyticsDateLocal = DateOnly.FromDateTime(DateTime.UtcNow) });
            await context.SaveChangesAsync();

            await service.ComputeAndPersistSummaryAsync(task.TaskId);

            var segments = await taskRepo.GetFocusSegmentsForTaskAsync(task.TaskId);
            segments.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task DeleteDistractionEvents_AfterSummarization()
    {
        var (context, taskRepo, eventRepo, service) = CreateSetup();
        using (context)
        {
            var task = await taskRepo.AddTaskAsync("Test", null);
            await taskRepo.SetActiveAsync(task.TaskId);
            await eventRepo.AddAsync(new DistractionEvent { TaskId = task.TaskId, OccurredAtUtc = DateTime.UtcNow, ProcessName = "chrome", DistractedDurationSecondsAtEmit = 10 });

            await service.ComputeAndPersistSummaryAsync(task.TaskId);

            var events = await eventRepo.GetEventsForTaskAsync(task.TaskId);
            events.Should().BeEmpty();
        }
    }
}

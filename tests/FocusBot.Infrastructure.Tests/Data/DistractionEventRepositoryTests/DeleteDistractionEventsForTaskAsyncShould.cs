using FocusBot.Infrastructure.Data;
using FocusBot.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FocusBot.Infrastructure.Tests.Data.DistractionEventRepositoryTests;

public class DeleteDistractionEventsForTaskAsyncShould
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
    public async Task DeleteAllEvents_ForGivenTask()
    {
        // Arrange
        using var context = CreateContext();
        var repo = new DistractionEventRepository(context);
        var now = DateTime.UtcNow;

        var event1 = new Core.Entities.DistractionEvent
        {
            TaskId = "task-1",
            OccurredAtUtc = now,
            ProcessName = "App1",
            DistractedDurationSecondsAtEmit = 10
        };
        var event2 = new Core.Entities.DistractionEvent
        {
            TaskId = "task-1",
            OccurredAtUtc = now.AddSeconds(5),
            ProcessName = "App2",
            DistractedDurationSecondsAtEmit = 15
        };

        context.DistractionEvents.Add(event1);
        context.DistractionEvents.Add(event2);
        await context.SaveChangesAsync();

        // Act
        await repo.DeleteDistractionEventsForTaskAsync("task-1");

        // Assert
        var remaining = await context.DistractionEvents.ToListAsync();
        remaining.Should().BeEmpty();
    }

    [Fact]
    public async Task NotAffectOtherTasks_WhenDeleting()
    {
        // Arrange
        using var context = CreateContext();
        var repo = new DistractionEventRepository(context);
        var now = DateTime.UtcNow;

        var event1 = new Core.Entities.DistractionEvent
        {
            TaskId = "task-1",
            OccurredAtUtc = now,
            ProcessName = "App1",
            DistractedDurationSecondsAtEmit = 10
        };
        var event2 = new Core.Entities.DistractionEvent
        {
            TaskId = "task-2",
            OccurredAtUtc = now.AddSeconds(5),
            ProcessName = "App2",
            DistractedDurationSecondsAtEmit = 15
        };

        context.DistractionEvents.Add(event1);
        context.DistractionEvents.Add(event2);
        await context.SaveChangesAsync();

        // Act
        await repo.DeleteDistractionEventsForTaskAsync("task-1");

        // Assert
        var remaining = await context.DistractionEvents.ToListAsync();
        remaining.Should().HaveCount(1);
        remaining[0].TaskId.Should().Be("task-2");
    }
}

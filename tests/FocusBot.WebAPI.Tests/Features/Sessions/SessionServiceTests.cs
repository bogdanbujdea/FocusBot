using FocusBot.WebAPI.Data;
using FocusBot.WebAPI.Data.Entities;
using FocusBot.WebAPI.Features.Sessions;
using Microsoft.EntityFrameworkCore;

namespace FocusBot.WebAPI.Tests.Features.Sessions;

public class SessionServiceTests
{
    private static ApiDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<ApiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApiDbContext(options);
    }

    [Fact]
    public async Task StartSessionAsync_CreatesNewSession()
    {
        await using var db = CreateInMemoryDb();
        var service = new SessionService(db);
        var userId = Guid.NewGuid();
        var request = new StartSessionRequest("Write tests", "Unit tests only", DeviceId: null);

        var result = await service.StartSessionAsync(userId, request);

        result.StatusCode.Should().Be(200);
        result.Session.Should().NotBeNull();
        result.Session!.TaskText.Should().Be("Write tests");
        result.Session.TaskHints.Should().Be("Unit tests only");
        result.Session.EndedAtUtc.Should().BeNull();
        (await db.Sessions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task StartSessionAsync_ReturnsConflict_WhenActiveSessionExists()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        db.Sessions.Add(new Session
        {
            UserId = userId,
            TaskText = "Existing session"
        });
        await db.SaveChangesAsync();

        var service = new SessionService(db);
        var result = await service.StartSessionAsync(userId, new StartSessionRequest("New session", null, DeviceId: null));

        result.StatusCode.Should().Be(409);
        result.Error.Should().Contain("active session");
        (await db.Sessions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task EndSessionAsync_UpdatesSessionWithSummaryData()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        db.Sessions.Add(new Session
        {
            Id = sessionId,
            UserId = userId,
            TaskText = "Deep work"
        });
        await db.SaveChangesAsync();

        var service = new SessionService(db);
        var endRequest = new EndSessionRequest(
            FocusScorePercent: 85,
            FocusedSeconds: 2400,
            DistractedSeconds: 300,
            DistractionCount: 5,
            ContextSwitchCount: 120,
            TopDistractingApps: "[\"Slack\"]",
            TopAlignedApps: null,
            DeviceId: null);

        var result = await service.EndSessionAsync(userId, sessionId, endRequest);

        result.StatusCode.Should().Be(200);
        result.Session.Should().NotBeNull();
        result.Session!.EndedAtUtc.Should().NotBeNull();
        result.Session.FocusScorePercent.Should().Be(85);
        result.Session.FocusedSeconds.Should().Be(2400);
        result.Session.DistractedSeconds.Should().Be(300);
        result.Session.DistractionCount.Should().Be(5);
        result.Session.ContextSwitchCount.Should().Be(120);
        result.Session.TopDistractingApps.Should().Be("[\"Slack\"]");
    }

    [Fact]
    public async Task GetSessionsAsync_ReturnsPaginatedResults()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        for (var i = 0; i < 5; i++)
        {
            db.Sessions.Add(new Session
            {
                UserId = userId,
                TaskText = $"Task {i}",
                StartedAtUtc = now.AddHours(-5 + i),
                EndedAtUtc = now.AddHours(-4 + i)
            });
        }
        await db.SaveChangesAsync();

        var service = new SessionService(db);
        var result = await service.GetSessionsAsync(userId, page: 1, pageSize: 2);

        result.TotalCount.Should().Be(5);
        result.Items.Should().HaveCount(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task PauseSessionAsync_UpdatesPausedAtUtc()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        db.Sessions.Add(new Session
        {
            Id = sessionId,
            UserId = userId,
            TaskText = "Deep work"
        });
        await db.SaveChangesAsync();

        var service = new SessionService(db);
        var result = await service.PauseSessionAsync(userId, sessionId);

        result.StatusCode.Should().Be(200);
        result.Session.Should().NotBeNull();
        result.Session!.PausedAtUtc.Should().NotBeNull();
        result.Session.IsPaused.Should().BeTrue();
        result.Session.TotalPausedSeconds.Should().Be(0);
    }

    [Fact]
    public async Task PauseSessionAsync_Returns409_WhenAlreadyPaused()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        db.Sessions.Add(new Session
        {
            Id = sessionId,
            UserId = userId,
            TaskText = "Deep work",
            PausedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new SessionService(db);
        var result = await service.PauseSessionAsync(userId, sessionId);

        result.StatusCode.Should().Be(409);
        result.Error.Should().Contain("already paused");
    }

    [Fact]
    public async Task ResumeSessionAsync_AccumulatesPauseDuration()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var pausedAt = DateTime.UtcNow.AddSeconds(-10);
        db.Sessions.Add(new Session
        {
            Id = sessionId,
            UserId = userId,
            TaskText = "Deep work",
            PausedAtUtc = pausedAt,
            TotalPausedSeconds = 0
        });
        await db.SaveChangesAsync();

        var service = new SessionService(db);
        var result = await service.ResumeSessionAsync(userId, sessionId);

        result.StatusCode.Should().Be(200);
        result.Session.Should().NotBeNull();
        result.Session!.PausedAtUtc.Should().BeNull();
        result.Session.IsPaused.Should().BeFalse();
        result.Session.TotalPausedSeconds.Should().BeGreaterThanOrEqualTo(10);
    }

    [Fact]
    public async Task ResumeSessionAsync_Returns409_WhenNotPaused()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        db.Sessions.Add(new Session
        {
            Id = sessionId,
            UserId = userId,
            TaskText = "Deep work"
        });
        await db.SaveChangesAsync();

        var service = new SessionService(db);
        var result = await service.ResumeSessionAsync(userId, sessionId);

        result.StatusCode.Should().Be(409);
        result.Error.Should().Contain("not paused");
    }

    [Fact]
    public async Task EndSessionAsync_AccumulatesPauseDuration_WhenPaused()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var pausedAt = DateTime.UtcNow.AddSeconds(-20);
        db.Sessions.Add(new Session
        {
            Id = sessionId,
            UserId = userId,
            TaskText = "Deep work",
            PausedAtUtc = pausedAt,
            TotalPausedSeconds = 10
        });
        await db.SaveChangesAsync();

        var service = new SessionService(db);
        var endRequest = new EndSessionRequest(
            FocusScorePercent: 80,
            FocusedSeconds: 1800,
            DistractedSeconds: 200,
            DistractionCount: 3,
            ContextSwitchCount: 60,
            TopDistractingApps: null,
            TopAlignedApps: null,
            DeviceId: null);

        var result = await service.EndSessionAsync(userId, sessionId, endRequest);

        result.StatusCode.Should().Be(200);
        result.Session.Should().NotBeNull();
        result.Session!.EndedAtUtc.Should().NotBeNull();
        result.Session.PausedAtUtc.Should().BeNull();
        result.Session.TotalPausedSeconds.Should().BeGreaterThanOrEqualTo(30); // 10 existing + ~20 from final pause
    }

    [Fact]
    public async Task MultiplePauseResumeCycles_AccumulateCorrectly()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        db.Sessions.Add(new Session
        {
            Id = sessionId,
            UserId = userId,
            TaskText = "Deep work"
        });
        await db.SaveChangesAsync();

        var service = new SessionService(db);

        // First pause cycle
        await service.PauseSessionAsync(userId, sessionId);
        await Task.Delay(1100); // Simulate 1.1 second pause
        var result1 = await service.ResumeSessionAsync(userId, sessionId);
        var pausedAfterFirstCycle = result1.Session!.TotalPausedSeconds;
        pausedAfterFirstCycle.Should().BeGreaterThan(0);

        // Second pause cycle
        await service.PauseSessionAsync(userId, sessionId);
        await Task.Delay(1100); // Simulate another 1.1 second pause
        var result2 = await service.ResumeSessionAsync(userId, sessionId);
        var pausedAfterSecondCycle = result2.Session!.TotalPausedSeconds;

        // Total should accumulate from both cycles
        pausedAfterSecondCycle.Should().BeGreaterThan(pausedAfterFirstCycle);
        result2.Session.PausedAtUtc.Should().BeNull();
        result2.Session.IsPaused.Should().BeFalse();
    }
}

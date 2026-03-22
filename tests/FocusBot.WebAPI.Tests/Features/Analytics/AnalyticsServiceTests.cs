using FocusBot.WebAPI.Data;
using FocusBot.WebAPI.Data.Entities;
using FocusBot.WebAPI.Features.Analytics;
using Microsoft.EntityFrameworkCore;

namespace FocusBot.WebAPI.Tests.Features.Analytics;

public class AnalyticsServiceTests
{
    private static ApiDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<ApiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApiDbContext(options);
    }

    private static void SeedCompletedSessions(ApiDbContext db, Guid userId, int count, DateTime baseDate)
    {
        for (var i = 0; i < count; i++)
        {
            db.Sessions.Add(new Session
            {
                UserId = userId,
                SessionTitle = $"Session {i}",
                StartedAtUtc = baseDate.AddHours(-count + i),
                EndedAtUtc = baseDate.AddHours(-count + i + 1),
                FocusScorePercent = 70 + i,
                FocusedSeconds = 1800 + i * 100,
                DistractedSeconds = 300 + i * 20,
                DistractionCount = 3 + i,
                ContextSwitchCount = 10 + i,
            });
        }
        db.SaveChanges();
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsZeros_WhenNoSessions()
    {
        await using var db = CreateInMemoryDb();
        var service = new AnalyticsService(db);
        var userId = Guid.NewGuid();

        var result = await service.GetSummaryAsync(userId, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow, null);

        result.TotalSessions.Should().Be(0);
        result.TotalFocusedSeconds.Should().Be(0);
        result.TotalDistractedSeconds.Should().Be(0);
        result.AverageFocusScorePercent.Should().Be(0);
    }

    [Fact]
    public async Task GetSummaryAsync_AggregatesCorrectly()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        SeedCompletedSessions(db, userId, 3, now);

        var service = new AnalyticsService(db);
        var result = await service.GetSummaryAsync(userId, now.AddDays(-1), now.AddDays(1), null);

        result.TotalSessions.Should().Be(3);
        result.TotalFocusedSeconds.Should().Be((1800L + 1900 + 2000));
        result.TotalDistractedSeconds.Should().Be((300L + 320 + 340));
        result.AverageFocusScorePercent.Should().Be(71);
        result.TotalDistractionCount.Should().Be(3 + 4 + 5);
        result.TotalContextSwitchCount.Should().Be(10 + 11 + 12);
    }

    [Fact]
    public async Task GetSummaryAsync_FiltersByClientId()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var clientA = Guid.NewGuid();
        var clientB = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Sessions.Add(new Session
        {
            UserId = userId, ClientId = clientA, SessionTitle = "A",
            StartedAtUtc = now.AddHours(-2), EndedAtUtc = now.AddHours(-1),
            FocusScorePercent = 80, FocusedSeconds = 3000, DistractedSeconds = 500,
        });
        db.Sessions.Add(new Session
        {
            UserId = userId, ClientId = clientB, SessionTitle = "B",
            StartedAtUtc = now.AddHours(-3), EndedAtUtc = now.AddHours(-2),
            FocusScorePercent = 60, FocusedSeconds = 1000, DistractedSeconds = 200,
        });
        await db.SaveChangesAsync();

        var service = new AnalyticsService(db);
        var result = await service.GetSummaryAsync(userId, now.AddDays(-1), now.AddDays(1), clientA);

        result.TotalSessions.Should().Be(1);
        result.TotalFocusedSeconds.Should().Be(3000);
    }

    [Fact]
    public async Task GetSummaryAsync_DoesNotIncludeOtherUsersData()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        SeedCompletedSessions(db, userId, 2, now);
        SeedCompletedSessions(db, otherUserId, 3, now);

        var service = new AnalyticsService(db);
        var result = await service.GetSummaryAsync(userId, now.AddDays(-1), now.AddDays(1), null);

        result.TotalSessions.Should().Be(2);
    }

    [Fact]
    public async Task GetTrendsAsync_GroupsByDay()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow.Date.AddHours(12);

        db.Sessions.Add(new Session
        {
            UserId = userId, SessionTitle = "Today",
            StartedAtUtc = now.AddHours(-2), EndedAtUtc = now.AddHours(-1),
            FocusScorePercent = 80, FocusedSeconds = 3000, DistractedSeconds = 500,
            DistractionCount = 5,
        });
        db.Sessions.Add(new Session
        {
            UserId = userId, SessionTitle = "Yesterday",
            StartedAtUtc = now.AddDays(-1).AddHours(-2), EndedAtUtc = now.AddDays(-1).AddHours(-1),
            FocusScorePercent = 70, FocusedSeconds = 2000, DistractedSeconds = 300,
            DistractionCount = 3,
        });
        await db.SaveChangesAsync();

        var service = new AnalyticsService(db);
        var result = await service.GetTrendsAsync(userId, now.AddDays(-3), now.AddDays(1), "daily", null);

        result.Granularity.Should().Be("daily");
        result.DataPoints.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetClientBreakdownAsync_GroupsByClient()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Clients.Add(new Client
        {
            Id = clientId, UserId = userId, Name = "Work Laptop",
            ClientType = ClientType.Desktop, Fingerprint = "fp-1",
        });
        db.Sessions.Add(new Session
        {
            UserId = userId, ClientId = clientId, SessionTitle = "Session",
            StartedAtUtc = now.AddHours(-2), EndedAtUtc = now.AddHours(-1),
            FocusScorePercent = 80, FocusedSeconds = 3000, DistractedSeconds = 500,
        });
        await db.SaveChangesAsync();

        var service = new AnalyticsService(db);
        var result = await service.GetClientBreakdownAsync(userId, now.AddDays(-1), now.AddDays(1));

        result.Clients.Should().HaveCount(1);
        result.Clients[0].Name.Should().Be("Work Laptop");
        result.Clients[0].Sessions.Should().Be(1);
        result.Clients[0].FocusedSeconds.Should().Be(3000);
    }

    [Fact]
    public async Task GetSummaryAsync_ExcludesActiveSessions()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Sessions.Add(new Session
        {
            UserId = userId, SessionTitle = "Active",
            StartedAtUtc = now.AddHours(-1),
            FocusScorePercent = 90, FocusedSeconds = 3000,
        });
        db.Sessions.Add(new Session
        {
            UserId = userId, SessionTitle = "Completed",
            StartedAtUtc = now.AddHours(-3), EndedAtUtc = now.AddHours(-2),
            FocusScorePercent = 80, FocusedSeconds = 2000, DistractedSeconds = 200,
        });
        await db.SaveChangesAsync();

        var service = new AnalyticsService(db);
        var result = await service.GetSummaryAsync(userId, now.AddDays(-1), now.AddDays(1), null);

        result.TotalSessions.Should().Be(1);
        result.TotalFocusedSeconds.Should().Be(2000);
    }
}

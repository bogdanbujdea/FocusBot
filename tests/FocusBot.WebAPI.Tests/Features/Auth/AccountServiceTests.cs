using FocusBot.WebAPI.Data;
using FocusBot.WebAPI.Data.Entities;
using FocusBot.WebAPI.Features.Auth;
using Microsoft.EntityFrameworkCore;

namespace FocusBot.WebAPI.Tests.Features.Auth;

public class AccountServiceTests
{
    private static ApiDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<ApiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApiDbContext(options);
    }

    [Fact]
    public async Task DeleteAccountAsync_RemovesAllUserData()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();

        db.Users.Add(new User { Id = userId, Email = "test@foqus.me" });
        db.Sessions.Add(new Session { UserId = userId, SessionTitle = "Test session" });
        db.Subscriptions.Add(new Subscription { UserId = userId, Status = SubscriptionStatus.Active });
        db.Clients.Add(new Client
        {
            UserId = userId, Name = "Desktop", Fingerprint = "fp",
            ClientType = ClientType.Desktop,
        });
        db.ClassificationCaches.Add(new ClassificationCache
        {
            UserId = userId, ContextHash = "h1", TaskContentHash = "h2",
            Score = 80, Reason = "test",
        });
        await db.SaveChangesAsync();

        var service = new AccountService(db);
        await service.DeleteAccountAsync(userId);

        (await db.Users.CountAsync()).Should().Be(0);
        (await db.Sessions.CountAsync()).Should().Be(0);
        (await db.Subscriptions.CountAsync()).Should().Be(0);
        (await db.Clients.CountAsync()).Should().Be(0);
        (await db.ClassificationCaches.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DeleteAccountAsync_DoesNotAffectOtherUsers()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        db.Users.Add(new User { Id = userId, Email = "test@foqus.me" });
        db.Users.Add(new User { Id = otherUserId, Email = "other@foqus.me" });
        db.Sessions.Add(new Session { UserId = userId, SessionTitle = "My session" });
        db.Sessions.Add(new Session { UserId = otherUserId, SessionTitle = "Their session" });
        await db.SaveChangesAsync();

        var service = new AccountService(db);
        await service.DeleteAccountAsync(userId);

        (await db.Users.CountAsync()).Should().Be(1);
        (await db.Sessions.CountAsync()).Should().Be(1);
        (await db.Sessions.FirstAsync()).SessionTitle.Should().Be("Their session");
    }

    [Fact]
    public async Task DeleteAccountAsync_HandlesNonExistentUser()
    {
        await using var db = CreateInMemoryDb();
        var service = new AccountService(db);

        var act = () => service.DeleteAccountAsync(Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }
}

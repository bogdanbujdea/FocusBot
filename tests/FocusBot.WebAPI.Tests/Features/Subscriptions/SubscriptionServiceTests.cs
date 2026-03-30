using FocusBot.WebAPI.Data;
using FocusBot.WebAPI.Data.Entities;
using FocusBot.WebAPI.Features.Pricing;
using FocusBot.WebAPI.Features.Subscriptions;
using FocusBot.WebAPI.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace FocusBot.WebAPI.Tests.Features.Subscriptions;

public class SubscriptionServiceTests
{
    private static ApiDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<ApiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApiDbContext(options);
    }

    private static SubscriptionService CreateService(ApiDbContext db)
    {
        var hub = CreateHubMock();
        var paddle = Mock.Of<IPaddleBillingApi>();
        var logger = Mock.Of<ILogger<SubscriptionService>>();
        return new SubscriptionService(db, hub.Object, paddle, logger);
    }

    private static void SeedUser(ApiDbContext db, Guid userId, string email = "test@example.com")
    {
        db.Users.Add(
            new User
            {
                Id = userId,
                Email = email,
                CreatedAtUtc = DateTime.UtcNow,
            }
        );
    }

    private static Mock<IHubContext<FocusHub, IFocusHubClient>> CreateHubMock()
    {
        var clients = new Mock<IHubClients<IFocusHubClient>>();
        var typedClient = new Mock<IFocusHubClient>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(typedClient.Object);
        var hub = new Mock<IHubContext<FocusHub, IFocusHubClient>>();
        hub.Setup(h => h.Clients).Returns(clients.Object);
        return hub;
    }

    [Fact]
    public async Task GetStatusAsync_AutoCreatesTrialForNewUser()
    {
        await using var db = CreateInMemoryDb();
        var service = CreateService(db);
        var userId = Guid.NewGuid();
        SeedUser(db, userId);
        await db.SaveChangesAsync();

        var result = await service.GetStatusAsync(userId);

        result.Should().NotBeNull();
        result!.Status.Should().Be(SubscriptionStatus.Trial);
        result.PlanType.Should().Be(PlanType.TrialFullAccess);
        result.TrialEndsAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromSeconds(5));
        (await db.Subscriptions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsNull_WhenUserNotProvisioned()
    {
        await using var db = CreateInMemoryDb();
        var service = CreateService(db);
        var userId = Guid.NewGuid();

        var result = await service.GetStatusAsync(userId);

        result.Should().BeNull();
        (await db.Subscriptions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task GetStatusAsync_IsIdempotent_DoesNotDuplicateTrial()
    {
        await using var db = CreateInMemoryDb();
        var service = CreateService(db);
        var userId = Guid.NewGuid();
        SeedUser(db, userId);
        await db.SaveChangesAsync();

        await service.GetStatusAsync(userId);
        await service.GetStatusAsync(userId);

        (await db.Subscriptions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsExistingSubscription_WhenRowExists()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        SeedUser(db, userId);
        db.Subscriptions.Add(new Subscription
        {
            UserId = userId,
            Status = SubscriptionStatus.Active,
            PlanType = PlanType.CloudManaged,
            PaddleSubscriptionId = "sub_123"
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetStatusAsync(userId);

        result.Should().NotBeNull();
        result!.Status.Should().Be(SubscriptionStatus.Active);
        result.PlanType.Should().Be(PlanType.CloudManaged);
        (await db.Subscriptions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ActivateTrialAsync_CreatesTrialSubscription()
    {
        await using var db = CreateInMemoryDb();
        var service = CreateService(db);
        var userId = Guid.NewGuid();
        SeedUser(db, userId);
        await db.SaveChangesAsync();

        var outcome = await service.ActivateTrialAsync(userId, PlanType.CloudBYOK);

        outcome.Kind.Should().Be(ActivateTrialResultKind.Created);
        outcome.Response.Should().NotBeNull();
        outcome.Response!.Status.Should().Be(SubscriptionStatus.Trial);
        outcome.Response.TrialEndsAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromSeconds(5));
        (await db.Subscriptions.CountAsync()).Should().Be(1);

        var row = await db.Subscriptions.SingleAsync();
        row.PlanType.Should().Be(PlanType.CloudBYOK);
    }

    [Fact]
    public async Task ActivateTrialAsync_ReturnsUserNotProvisioned_WhenUserMissing()
    {
        await using var db = CreateInMemoryDb();
        var service = CreateService(db);
        var userId = Guid.NewGuid();

        var outcome = await service.ActivateTrialAsync(userId, PlanType.CloudBYOK);

        outcome.Kind.Should().Be(ActivateTrialResultKind.UserNotProvisioned);
        outcome.Response.Should().BeNull();
        (await db.Subscriptions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ActivateTrialAsync_ReturnsAlreadyExists_WhenTrialAlreadyActivated()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        SeedUser(db, userId);
        db.Subscriptions.Add(new Subscription
        {
            UserId = userId,
            Status = SubscriptionStatus.Trial,
            TrialEndsAtUtc = DateTime.UtcNow.AddHours(24)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var outcome = await service.ActivateTrialAsync(userId, PlanType.CloudBYOK);

        outcome.Kind.Should().Be(ActivateTrialResultKind.AlreadyExists);
        outcome.Response.Should().BeNull();
        (await db.Subscriptions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task IsSubscribedOrTrialActiveAsync_ReturnsTrue_ForTrialFullAccess()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        db.Subscriptions.Add(new Subscription
        {
            UserId = userId,
            Status = SubscriptionStatus.Trial,
            PlanType = PlanType.TrialFullAccess,
            TrialEndsAtUtc = DateTime.UtcNow.AddHours(20)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.IsSubscribedOrTrialActiveAsync(userId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsSubscribedOrTrialActiveAsync_ReturnsTrue_ForActiveSubscription()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        db.Subscriptions.Add(new Subscription
        {
            UserId = userId,
            Status = SubscriptionStatus.Active,
            PaddleSubscriptionId = "sub_123"
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.IsSubscribedOrTrialActiveAsync(userId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsSubscribedOrTrialActiveAsync_ReturnsTrue_ForActiveTrial()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        db.Subscriptions.Add(new Subscription
        {
            UserId = userId,
            Status = SubscriptionStatus.Trial,
            TrialEndsAtUtc = DateTime.UtcNow.AddHours(12)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.IsSubscribedOrTrialActiveAsync(userId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsSubscribedOrTrialActiveAsync_ReturnsFalse_ForExpiredTrial()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        db.Subscriptions.Add(new Subscription
        {
            UserId = userId,
            Status = SubscriptionStatus.Trial,
            TrialEndsAtUtc = DateTime.UtcNow.AddHours(-1)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.IsSubscribedOrTrialActiveAsync(userId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HandleSubscriptionCreatedAsync_SetsPlanTypeAndEnrichment()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        SeedUser(db, userId);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var sub = new PaddleSubscription
        {
            Id = "sub_test_1",
            Status = "active",
            CustomerId = "ctm_test",
            CustomData = new PaddleCustomData { UserId = userId.ToString(), PlanType = "cloud-managed" },
            Items = new List<PaddleSubscriptionItem>
            {
                new PaddleSubscriptionItem
                {
                    Price = new PaddlePrice
                    {
                        Id = "pri_1",
                        ProductId = "pro_1",
                        UnitPrice = new PaddleUnitPrice { Amount = "499", CurrencyCode = "USD" },
                        BillingCycle = new PaddleBillingCycle { Interval = "month", Frequency = 1 },
                        CustomData = new PaddlePriceCustomData { PlanType = "cloud-managed" }
                    }
                }
            },
            CurrentBillingPeriod = new PaddleBillingPeriod { EndsAt = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            NextBilledAt = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        await service.HandleSubscriptionCreatedAsync(sub, "evt_test_1", DateTime.UtcNow);

        var row = await db.Subscriptions.SingleAsync(s => s.UserId == userId);
        row.Status.Should().Be(SubscriptionStatus.Active);
        row.PlanType.Should().Be(PlanType.CloudManaged);
        row.PaddleSubscriptionId.Should().Be("sub_test_1");
        row.PaddleCustomerId.Should().Be("ctm_test");
        row.PaddlePriceId.Should().Be("pri_1");
        row.PaddleProductId.Should().Be("pro_1");
        row.CurrencyCode.Should().Be("USD");
        row.UnitAmountMinor.Should().Be(499);
        row.BillingInterval.Should().Be("month");
        row.CurrentPeriodEndsAtUtc.Should().NotBeNull();
        row.NextBilledAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task GetStatusAsync_IncludesNextBilledAt()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var next = DateTime.UtcNow.AddDays(30);
        SeedUser(db, userId);
        db.Subscriptions.Add(new Subscription
        {
            UserId = userId,
            Status = SubscriptionStatus.Active,
            PlanType = PlanType.CloudBYOK,
            NextBilledAtUtc = next,
            PaddleSubscriptionId = "sub_x"
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var status = await service.GetStatusAsync(userId);

        status.Should().NotBeNull();
        status!.NextBilledAtUtc.Should().Be(next);
    }

    [Fact]
    public async Task HandleSubscriptionCreatedAsync_IsIdempotent()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        SeedUser(db, userId);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var sub = BuildTestSubscription(userId, "sub_idempotent_1", "cloud-byok");

        await service.HandleSubscriptionCreatedAsync(sub, "evt_idempotent_1", DateTime.UtcNow);
        await service.HandleSubscriptionCreatedAsync(sub, "evt_idempotent_1", DateTime.UtcNow);

        (await db.Subscriptions.CountAsync()).Should().Be(1);
        (await db.Set<ProcessedWebhookEvent>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task HandleSubscriptionCreatedAsync_UpgradesTrialToActive()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        SeedUser(db, userId);
        var service = CreateService(db);

        db.Subscriptions.Add(new Subscription
        {
            UserId = userId,
            Status = SubscriptionStatus.Trial,
            PlanType = PlanType.TrialFullAccess,
            TrialEndsAtUtc = DateTime.UtcNow.AddHours(12)
        });
        await db.SaveChangesAsync();

        var sub = BuildTestSubscription(userId, "sub_upgrade_1", "cloud-managed");

        await service.HandleSubscriptionCreatedAsync(sub, "evt_upgrade_1", DateTime.UtcNow);

        var row = await db.Subscriptions.SingleAsync(s => s.UserId == userId);
        row.Status.Should().Be(SubscriptionStatus.Active);
        row.PlanType.Should().Be(PlanType.CloudManaged);
        row.PaddleSubscriptionId.Should().Be("sub_upgrade_1");
    }

    [Fact]
    public async Task HandleTransactionCompletedAsync_MergesIntoExistingRow_WhenTrialExistsWithoutPaddleId()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        SeedUser(db, userId);
        db.Subscriptions.Add(
            new Subscription
            {
                UserId = userId,
                Status = SubscriptionStatus.Trial,
                PlanType = PlanType.TrialFullAccess,
                TrialEndsAtUtc = DateTime.UtcNow.AddHours(12),
            }
        );
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var txn = new PaddleTransaction
        {
            Id = "txn_merge_1",
            SubscriptionId = "sub_from_txn",
            CustomerId = "ctm_merge",
            CustomData = new PaddleCustomData
            {
                UserId = userId.ToString(),
                PlanType = "cloud-byok",
            },
            Items =
            [
                new PaddleTransactionItem
                {
                    Price = new PaddlePrice { Id = "pri_1", ProductId = "pro_1" },
                },
            ],
        };

        await service.HandleTransactionCompletedAsync(txn, "evt_txn_merge_1");

        (await db.Subscriptions.CountAsync()).Should().Be(1);
        var row = await db.Subscriptions.SingleAsync();
        row.PaddleSubscriptionId.Should().Be("sub_from_txn");
        row.PaddleCustomerId.Should().Be("ctm_merge");
        row.Status.Should().Be(SubscriptionStatus.Active);
        row.PlanType.Should().Be(PlanType.CloudBYOK);
    }

    [Fact]
    public async Task HandleSubscriptionCreatedAsync_SkipsWhenPlanTypeUnresolvable()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var service = CreateService(db);

        var sub = new PaddleSubscription
        {
            Id = "sub_no_plan",
            Status = "active",
            CustomerId = "ctm_test",
            CustomData = new PaddleCustomData { UserId = userId.ToString() },
            Items = new List<PaddleSubscriptionItem>()
        };

        await service.HandleSubscriptionCreatedAsync(sub, "evt_no_plan", DateTime.UtcNow);

        (await db.Subscriptions.CountAsync()).Should().Be(0);
        (await db.ProcessedWebhookEvents.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task HandleSubscriptionCreatedAsync_SkipsWhenUserNotProvisioned()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var service = CreateService(db);

        var sub = BuildTestSubscription(userId, "sub_no_user", "cloud-byok");

        await service.HandleSubscriptionCreatedAsync(sub, "evt_no_user", DateTime.UtcNow);

        (await db.Subscriptions.CountAsync()).Should().Be(0);
        (await db.ProcessedWebhookEvents.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task HandleSubscriptionCanceledAsync_SetsCanceledStatusAndTimestamp()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var service = CreateService(db);

        db.Subscriptions.Add(new Subscription
        {
            UserId = userId,
            PaddleSubscriptionId = "sub_cancel_1",
            Status = SubscriptionStatus.Active,
            PlanType = PlanType.CloudBYOK
        });
        await db.SaveChangesAsync();

        var canceledAt = DateTime.UtcNow.AddHours(-1);
        var sub = new PaddleSubscription
        {
            Id = "sub_cancel_1",
            Status = "canceled",
            CustomerId = "ctm_test",
            CanceledAt = canceledAt,
            ScheduledChange = new PaddleScheduledChange { Action = "cancel" }
        };

        await service.HandleSubscriptionCanceledAsync(sub, "evt_cancel_1");

        var row = await db.Subscriptions.SingleAsync(s => s.UserId == userId);
        row.Status.Should().Be(SubscriptionStatus.Canceled);
        row.CancelledAtUtc.Should().Be(canceledAt.ToUniversalTime());
        row.CancellationReason.Should().Be("cancel");
    }

    [Fact]
    public async Task HandleSubscriptionCanceledAsync_IsIdempotent()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var service = CreateService(db);

        db.Subscriptions.Add(new Subscription
        {
            UserId = userId,
            PaddleSubscriptionId = "sub_cancel_idempotent",
            Status = SubscriptionStatus.Active
        });
        await db.SaveChangesAsync();

        var sub = new PaddleSubscription
        {
            Id = "sub_cancel_idempotent",
            Status = "canceled",
            CanceledAt = DateTime.UtcNow
        };

        await service.HandleSubscriptionCanceledAsync(sub, "evt_cancel_idempotent");
        await service.HandleSubscriptionCanceledAsync(sub, "evt_cancel_idempotent");

        (await db.ProcessedWebhookEvents.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task MapSubscriptionStatus_PastDueMapToExpired()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        SeedUser(db, userId);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var sub = new PaddleSubscription
        {
            Id = "sub_past_due",
            Status = "past_due",
            CustomerId = "ctm_test",
            CustomData = new PaddleCustomData { UserId = userId.ToString(), PlanType = "cloud-byok" },
            Items = new List<PaddleSubscriptionItem>
            {
                new PaddleSubscriptionItem
                {
                    Price = new PaddlePrice
                    {
                        Id = "pri_1",
                        ProductId = "pro_1",
                        UnitPrice = new PaddleUnitPrice { Amount = "199", CurrencyCode = "USD" },
                        BillingCycle = new PaddleBillingCycle { Interval = "month", Frequency = 1 }
                    }
                }
            }
        };

        await service.HandleSubscriptionCreatedAsync(sub, "evt_past_due", DateTime.UtcNow);

        var row = await db.Subscriptions.SingleAsync(s => s.UserId == userId);
        row.Status.Should().Be(SubscriptionStatus.Expired);
    }

    [Fact]
    public async Task ActivateTrialAsync_WithDifferentPlanTypes()
    {
        await using var db = CreateInMemoryDb();
        var service = CreateService(db);

        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        SeedUser(db, userId1);
        SeedUser(db, userId2, "other@example.com");
        await db.SaveChangesAsync();

        var outcome1 = await service.ActivateTrialAsync(userId1, PlanType.CloudBYOK);
        var outcome2 = await service.ActivateTrialAsync(userId2, PlanType.CloudManaged);

        outcome1.Kind.Should().Be(ActivateTrialResultKind.Created);
        outcome2.Kind.Should().Be(ActivateTrialResultKind.Created);

        var row1 = await db.Subscriptions.SingleAsync(s => s.UserId == userId1);
        var row2 = await db.Subscriptions.SingleAsync(s => s.UserId == userId2);

        row1.PlanType.Should().Be(PlanType.CloudBYOK);
        row2.PlanType.Should().Be(PlanType.CloudManaged);
    }

    [Fact]
    public void PaddleWebhookVerifier_ReturnsFalse_WhenWebhookSecretIsEmpty()
    {
        var ok = PaddleWebhookVerifier.TryVerify("{}", null, "", out var err);
        ok.Should().BeFalse();
        err.Should().Contain("not configured");
    }

    [Fact]
    public void PaddleWebhookVerifier_ReturnsFalse_WhenWebhookSecretIsNull()
    {
        var ok = PaddleWebhookVerifier.TryVerify("{}", null, null, out var err);
        ok.Should().BeFalse();
        err.Should().Contain("not configured");
    }

    private static PaddleSubscription BuildTestSubscription(Guid userId, string subscriptionId, string planType)
    {
        return new PaddleSubscription
        {
            Id = subscriptionId,
            Status = "active",
            CustomerId = "ctm_test",
            CustomData = new PaddleCustomData { UserId = userId.ToString(), PlanType = planType },
            Items = new List<PaddleSubscriptionItem>
            {
                new PaddleSubscriptionItem
                {
                    Price = new PaddlePrice
                    {
                        Id = "pri_1",
                        ProductId = "pro_1",
                        UnitPrice = new PaddleUnitPrice { Amount = "199", CurrencyCode = "USD" },
                        BillingCycle = new PaddleBillingCycle { Interval = "month", Frequency = 1 },
                        CustomData = new PaddlePriceCustomData { PlanType = planType }
                    }
                }
            },
            CurrentBillingPeriod = new PaddleBillingPeriod { EndsAt = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            NextBilledAt = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
    }
}

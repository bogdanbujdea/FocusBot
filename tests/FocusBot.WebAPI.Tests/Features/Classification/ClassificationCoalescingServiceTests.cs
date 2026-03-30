using FocusBot.WebAPI.Data;
using FocusBot.WebAPI.Data.Entities;
using FocusBot.WebAPI.Features.Classification;
using FocusBot.WebAPI.Features.Clients;
using FocusBot.WebAPI.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FocusBot.WebAPI.Tests.Features.Classification;

public class ClassificationCoalescingServiceTests
{
    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    [Fact]
    public async Task CoalescesRequestsFromSameUserIntoOneClassification()
    {
        var harness = CreateHarness();

        var desktop = DesktopBrowserRequest("A");
        var extension = ExtensionRequest("B");

        var first = harness.CoalescingService.EnqueueAndWaitAsync(
            TestUserId,
            desktop,
            "byok",
            null,
            CancellationToken.None
        );
        var second = harness.CoalescingService.EnqueueAndWaitAsync(
            TestUserId,
            extension,
            "byok",
            null,
            CancellationToken.None
        );

        var results = await Task.WhenAll(first, second);

        harness.ClassificationService.LlmCallCount.Should().Be(1);
        results[0].Reason.Should().Be(results[1].Reason);
    }

    [Fact]
    public async Task PrefersNonBrowserDesktopOverExtension()
    {
        var harness = CreateHarness();

        var extension = ExtensionRequest("browser");
        var desktopNonBrowser = NonBrowserDesktopRequest("docker");

        var first = harness.CoalescingService.EnqueueAndWaitAsync(
            TestUserId,
            extension,
            "byok",
            null,
            CancellationToken.None
        );
        var second = harness.CoalescingService.EnqueueAndWaitAsync(
            TestUserId,
            desktopNonBrowser,
            "byok",
            null,
            CancellationToken.None
        );

        var response = await first;
        await second;

        response.Reason.Should().Contain("desktop-non-browser");
    }

    [Fact]
    public async Task PrefersExtensionOverDesktopBrowser()
    {
        var harness = CreateHarness();

        var desktopBrowser = DesktopBrowserRequest("desktop-browser");
        var extension = ExtensionRequest("extension");

        var first = harness.CoalescingService.EnqueueAndWaitAsync(
            TestUserId,
            desktopBrowser,
            "byok",
            null,
            CancellationToken.None
        );
        var second = harness.CoalescingService.EnqueueAndWaitAsync(
            TestUserId,
            extension,
            "byok",
            null,
            CancellationToken.None
        );

        var response = await first;
        await second;

        response.Reason.Should().Contain("extension");
    }

    [Fact]
    public async Task KeepsQueuesIndependentPerUser()
    {
        var harness = CreateHarness();

        var first = harness.CoalescingService.EnqueueAndWaitAsync(
            TestUserId,
            ExtensionRequest("user-a"),
            "byok",
            null,
            CancellationToken.None
        );
        var second = harness.CoalescingService.EnqueueAndWaitAsync(
            OtherUserId,
            ExtensionRequest("user-b"),
            "byok",
            null,
            CancellationToken.None
        );

        var results = await Task.WhenAll(first, second);

        harness.ClassificationService.LlmCallCount.Should().Be(2);
        results[0].Reason.Should().Contain("user-a");
        results[1].Reason.Should().Contain("user-b");
    }

    [Fact]
    public async Task HonorsRequestCancellation()
    {
        var harness = CreateHarness();
        using var cts = new CancellationTokenSource();

        var task = harness.CoalescingService.EnqueueAndWaitAsync(
            TestUserId,
            ExtensionRequest("cancel"),
            "byok",
            null,
            cts.Token
        );

        cts.Cancel();

        var act = async () => await task;
        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    [Fact]
    public async Task PropagatesClassifierErrorsToAllPendingRequests()
    {
        var harness = CreateHarness(failClassification: true);

        var first = harness.CoalescingService.EnqueueAndWaitAsync(
            TestUserId,
            DesktopBrowserRequest("one"),
            "byok",
            null,
            CancellationToken.None
        );
        var second = harness.CoalescingService.EnqueueAndWaitAsync(
            TestUserId,
            ExtensionRequest("two"),
            "byok",
            null,
            CancellationToken.None
        );

        var firstAct = async () => await first;
        var secondAct = async () => await second;
        await firstAct.Should().ThrowAsync<InvalidOperationException>();
        await secondAct.Should().ThrowAsync<InvalidOperationException>();
        harness.ClassificationService.LlmCallCount.Should().Be(1);
    }

    [Fact]
    public async Task BroadcastsClassificationChangedAfterSuccessfulClassification()
    {
        var harness = CreateHarness();

        await harness.CoalescingService.EnqueueAndWaitAsync(
            TestUserId,
            ExtensionRequest("broadcast-test"),
            "byok",
            null,
            CancellationToken.None);

        harness.HubClientMock.Verify(
            c => c.ClassificationChanged(It.Is<ClassificationChangedEvent>(e => e.Source == "extension" && e.Score == 7)),
            Times.Once);
    }

    [Fact]
    public async Task DoesNotBroadcastClassificationChangedWhenClassificationFails()
    {
        var harness = CreateHarness(failClassification: true);

        var first = harness.CoalescingService.EnqueueAndWaitAsync(
            TestUserId,
            ExtensionRequest("fail-broadcast"),
            "byok",
            null,
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => first);

        harness.HubClientMock.Verify(
            c => c.ClassificationChanged(It.IsAny<ClassificationChangedEvent>()),
            Times.Never);
    }

    private static TestHarness CreateHarness(bool failClassification = false)
    {
        var options = new DbContextOptionsBuilder<ApiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using (var seedDb = new TestDbContext(options))
        {
            seedDb.Users.AddRange(
                new User { Id = TestUserId, Email = "user-a@example.com" },
                new User { Id = OtherUserId, Email = "user-b@example.com" }
            );
            seedDb.SaveChanges();
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ManagedOpenAiKey"] = "managed-key",
                }
            )
            .Build();

        var probe = new ClassificationProbe(failClassification);

        var services = new ServiceCollection();
        services.AddSingleton(probe);
        services.AddScoped<ApiDbContext>(_ => new TestDbContext(options));
        services.AddScoped<ClassificationService>(sp =>
            new ObservingClassificationService(
                sp.GetRequiredService<ApiDbContext>(),
                config,
                NullLogger<ClassificationService>.Instance,
                sp.GetRequiredService<ClassificationProbe>()
            )
        );
        services.AddScoped<ClientService>();
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var hubClientMock = new Mock<IFocusHubClient>();
        hubClientMock
            .Setup(c => c.ClassificationChanged(It.IsAny<ClassificationChangedEvent>()))
            .Returns(Task.CompletedTask);

        var hubClients = new Mock<IHubClients<IFocusHubClient>>();
        hubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(hubClientMock.Object);

        var hubContext = new Mock<IHubContext<FocusHub, IFocusHubClient>>();
        hubContext.Setup(h => h.Clients).Returns(hubClients.Object);

        var coalescingService = new ClassificationCoalescingService(
            scopeFactory,
            hubContext.Object,
            NullLogger<ClassificationCoalescingService>.Instance
        );

        return new TestHarness(coalescingService, probe, hubClientMock);
    }

    private static ClassifyRequest ExtensionRequest(string marker) =>
        new(
            "Task",
            "Context",
            "msedge",
            $"Edge - {marker}",
            $"https://example.com/{marker}",
            $"Page {marker}",
            null,
            null,
            null
        );

    private static ClassifyRequest DesktopBrowserRequest(string marker) =>
        new(
            "Task",
            "Context",
            "msedge",
            $"Browser {marker}",
            null,
            null,
            null,
            null,
            null
        );

    private static ClassifyRequest NonBrowserDesktopRequest(string marker) =>
        new(
            "Task",
            "Context",
            "Docker Desktop",
            $"Docker {marker}",
            null,
            null,
            null,
            null,
            null
        );

    private sealed record TestHarness(
        ClassificationCoalescingService CoalescingService,
        ClassificationProbe ClassificationService,
        Mock<IFocusHubClient> HubClientMock);
}

internal sealed class ObservingClassificationService : ClassificationService
{
    private readonly ClassificationProbe _probe;

    public ObservingClassificationService(
        ApiDbContext db,
        IConfiguration configuration,
        ILogger<ClassificationService> logger,
        ClassificationProbe probe)
        : base(db, configuration, logger)
    {
        _probe = probe;
    }

    protected override Task<ClassifyResponse> CallLlmAsync(
        string apiKey,
        string providerId,
        string modelId,
        ClassifyRequest request,
        CancellationToken ct)
    {
        Interlocked.Increment(ref _probe.LlmCallCount);

        if (_probe.FailClassification)
        {
            throw new InvalidOperationException("Simulated classifier failure.");
        }

        var reason = request switch
        {
            { Url: not null and not "" } => $"extension:{request.Url}",
            { ProcessName: not null and not "" } when !IsBrowserProcess(request.ProcessName) =>
                $"desktop-non-browser:{request.ProcessName}",
            _ => $"desktop-browser:{request.WindowTitle}"
        };

        return Task.FromResult(new ClassifyResponse(7, reason, Cached: false));
    }

    private static bool IsBrowserProcess(string processName) =>
        processName.Equals("chrome", StringComparison.OrdinalIgnoreCase)
        || processName.Equals("msedge", StringComparison.OrdinalIgnoreCase)
        || processName.Equals("firefox", StringComparison.OrdinalIgnoreCase)
        || processName.Equals("brave", StringComparison.OrdinalIgnoreCase)
        || processName.Equals("opera", StringComparison.OrdinalIgnoreCase);
}

internal sealed class ClassificationProbe(bool failClassification)
{
    public int LlmCallCount;
    public bool FailClassification { get; } = failClassification;
}

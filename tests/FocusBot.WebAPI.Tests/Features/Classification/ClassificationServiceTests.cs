using FocusBot.WebAPI.Data;
using FocusBot.WebAPI.Data.Entities;
using FocusBot.WebAPI.Features.Classification;
using LlmTornado.Code;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FocusBot.WebAPI.Tests.Features.Classification;

/// <summary>
/// Test-specific DbContext that registers the ClassificationCache entity
/// (which is not yet wired in the production ApiDbContext).
/// </summary>
internal class TestDbContext : ApiDbContext
{
    public TestDbContext(DbContextOptions<ApiDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ClassificationCache>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId);
        });
    }
}

/// <summary>
/// Subclass that overrides the LLM call to return a controlled response.
/// </summary>
internal class TestableClassificationService : ClassificationService
{
    private readonly ClassifyResponse _stubbedResponse;

    public TestableClassificationService(
        ApiDbContext db,
        IConfiguration configuration,
        ILogger<ClassificationService> logger,
        ClassifyResponse stubbedResponse
    )
        : base(db, configuration, logger)
    {
        _stubbedResponse = stubbedResponse;
    }

    public int LlmCallCount { get; private set; }
    public string? LastApiKey { get; private set; }

    protected override Task<ClassifyResponse> CallLlmAsync(
        string apiKey,
        string providerId,
        string modelId,
        ClassifyRequest request,
        CancellationToken ct
    )
    {
        LlmCallCount++;
        LastApiKey = apiKey;
        return Task.FromResult(_stubbedResponse);
    }
}

public class ClassificationServiceTests
{
    private static readonly Guid TestUserId = Guid.NewGuid();

    private static (TestDbContext Db, TestableClassificationService Service) CreateService(
        ClassifyResponse? stubbedResponse = null,
        Dictionary<string, string?>? configOverrides = null
    )
    {
        var options = new DbContextOptionsBuilder<ApiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new TestDbContext(options);
        db.Users.Add(new User { Id = TestUserId, Email = "test@example.com" });
        db.SaveChanges();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configOverrides ?? new Dictionary<string, string?>())
            .Build();

        var logger = NullLogger<ClassificationService>.Instance;
        var response = stubbedResponse ?? new ClassifyResponse(8, "Relevant", false);
        var service = new TestableClassificationService(db, config, logger, response);

        return (db, service);
    }

    private static ClassifyRequest DefaultRequest() =>
        new(
            "Write quarterly report",
            "Google Docs",
            "msedge",
            "Q3 Report - Google Docs",
            null,
            null,
            null,
            null,
            null
        );

    // ── Cache hit ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReturnCachedResult_WhenCacheEntryExists()
    {
        // Arrange
        var (db, service) = CreateService();
        var request = DefaultRequest();
        var contextHash = ClassificationService.ComputeContextHash(
            request.ProcessName,
            request.WindowTitle,
            request.Url,
            request.PageTitle
        );
        var taskHash = ClassificationService.ComputeTaskContentHash(
            request.SessionTitle,
            request.SessionContext
        );

        db.Set<ClassificationCache>()
            .Add(
                new ClassificationCache
                {
                    UserId = TestUserId,
                    ContextHash = contextHash,
                    TaskContentHash = taskHash,
                    Score = 9,
                    Reason = "Cached reason",
                    ExpiresAtUtc = DateTime.UtcNow.AddHours(12),
                }
            );
        await db.SaveChangesAsync();

        // Act
        var result = await service.ClassifyAsync(TestUserId, request, byokApiKey: "test-key");

        // Assert
        result.Cached.Should().BeTrue();
        result.Score.Should().Be(9);
        result.Reason.Should().Be("Cached reason");
        service.LlmCallCount.Should().Be(0);
    }

    [Fact]
    public async Task NotReturnExpiredCacheEntry()
    {
        // Arrange
        var (db, service) = CreateService();
        var request = DefaultRequest();
        var contextHash = ClassificationService.ComputeContextHash(
            request.ProcessName,
            request.WindowTitle,
            request.Url,
            request.PageTitle
        );
        var taskHash = ClassificationService.ComputeTaskContentHash(
            request.SessionTitle,
            request.SessionContext
        );

        db.Set<ClassificationCache>()
            .Add(
                new ClassificationCache
                {
                    UserId = TestUserId,
                    ContextHash = contextHash,
                    TaskContentHash = taskHash,
                    Score = 9,
                    Reason = "Expired",
                    ExpiresAtUtc = DateTime.UtcNow.AddHours(-1),
                }
            );
        await db.SaveChangesAsync();

        // Act
        var result = await service.ClassifyAsync(TestUserId, request, byokApiKey: "test-key");

        // Assert
        result.Cached.Should().BeFalse();
        service.LlmCallCount.Should().Be(1);
    }

    // ── Cache miss ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CallLlm_WhenNoCacheEntryExists()
    {
        // Arrange
        var (_, service) = CreateService(
            stubbedResponse: new ClassifyResponse(7, "LLM result", false)
        );

        // Act
        var result = await service.ClassifyAsync(
            TestUserId,
            DefaultRequest(),
            byokApiKey: "test-key"
        );

        // Assert
        result.Cached.Should().BeFalse();
        result.Score.Should().Be(7);
        result.Reason.Should().Be("LLM result");
        service.LlmCallCount.Should().Be(1);
    }

    [Fact]
    public async Task CacheResultAfterLlmCall()
    {
        // Arrange
        var (db, service) = CreateService(
            stubbedResponse: new ClassifyResponse(7, "Fresh result", false)
        );

        // Act
        await service.ClassifyAsync(TestUserId, DefaultRequest(), byokApiKey: "test-key");

        // Assert
        var cached = await db.Set<ClassificationCache>().CountAsync();
        cached.Should().Be(1);
    }

    // ── BYOK mode ────────────────────────────────────────────────────────────

    [Fact]
    public async Task UseProvidedApiKey_WhenByokHeaderPresent()
    {
        // Arrange
        var (_, service) = CreateService();

        // Act
        await service.ClassifyAsync(TestUserId, DefaultRequest(), byokApiKey: "sk-user-key-123");

        // Assert
        service.LastApiKey.Should().Be("sk-user-key-123");
    }

    [Fact]
    public async Task UseRequestProviderAndModel_WhenByokHeaderPresent()
    {
        // Arrange
        var request = new ClassifyRequest(
            "Task",
            null,
            "code",
            "VS Code",
            null,
            null,
            "Anthropic",
            "claude-3-5-sonnet",
            null
        );
        var (_, service) = CreateService();

        // Act
        await service.ClassifyAsync(TestUserId, request, byokApiKey: "sk-user-key");

        // Assert
        service.LlmCallCount.Should().Be(1);
    }

    // ── Managed mode ─────────────────────────────────────────────────────────

    [Fact]
    public async Task UseManagedKey_WhenNoByokHeader()
    {
        // Arrange
        var (_, service) = CreateService(
            configOverrides: new Dictionary<string, string?>
            {
                ["ManagedOpenAiKey"] = "sk-managed-key",
            }
        );

        // Act
        await service.ClassifyAsync(TestUserId, DefaultRequest(), byokApiKey: null);

        // Assert
        service.LastApiKey.Should().Be("sk-managed-key");
    }

    [Fact]
    public async Task ThrowInvalidOperation_WhenNoApiKeyAvailable()
    {
        // Arrange
        var (_, service) = CreateService();

        // Act
        var act = async () =>
            await service.ClassifyAsync(TestUserId, DefaultRequest(), byokApiKey: null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*API key*");
    }

    // ── Static helpers ───────────────────────────────────────────────────────

    [Fact]
    public void ComputeHash_ReturnsDeterministicValue()
    {
        // Arrange
        var input = "test-input";

        // Act
        var hash1 = ClassificationService.ComputeHash(input);
        var hash2 = ClassificationService.ComputeHash(input);

        // Assert
        hash1.Should().Be(hash2);
        hash1.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ComputeHash_ReturnsDifferentValues_ForDifferentInputs()
    {
        // Arrange & Act
        var hash1 = ClassificationService.ComputeHash("input-a");
        var hash2 = ClassificationService.ComputeHash("input-b");

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ParseLlmResponse_ExtractsScoreAndReason()
    {
        // Arrange
        var json = """{"score": 8, "reason": "Directly related"}""";

        // Act
        var result = ClassificationService.ParseLlmResponse(json);

        // Assert
        result.Score.Should().Be(8);
        result.Reason.Should().Be("Directly related");
        result.Cached.Should().BeFalse();
    }

    [Fact]
    public void ParseLlmResponse_ClampsScoreToRange()
    {
        // Arrange
        var json = """{"score": 15, "reason": "Out of range"}""";

        // Act
        var result = ClassificationService.ParseLlmResponse(json);

        // Assert
        result.Score.Should().Be(10);
    }

    [Fact]
    public void ParseLlmResponse_ThrowsOnInvalidJson()
    {
        // Arrange
        var notJson = "this is not json";

        // Act
        var act = () => ClassificationService.ParseLlmResponse(notJson);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*invalid JSON*");
    }

    [Theory]
    [InlineData("OpenAi", LLmProviders.OpenAi)]
    [InlineData("Anthropic", LLmProviders.Anthropic)]
    [InlineData("Google", LLmProviders.Google)]
    [InlineData("Unknown", LLmProviders.OpenAi)]
    public void MapProvider_ReturnsCorrectEnum(string input, LLmProviders expected)
    {
        // Arrange & Act
        var result = ClassificationService.MapProvider(input);

        // Assert
        result.Should().Be(expected);
    }
}

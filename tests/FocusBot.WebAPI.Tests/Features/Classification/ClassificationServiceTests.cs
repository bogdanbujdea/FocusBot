using FocusBot.WebAPI.Features.Classification;
using LlmTornado.Code;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FocusBot.WebAPI.Tests.Features.Classification;

/// <summary>
/// Subclass that overrides the LLM call to return a controlled response.
/// </summary>
internal class TestableClassificationService : ClassificationService
{
    private readonly ClassifyResponse _stubbedResponse;

    public TestableClassificationService(
        IMemoryCache memoryCache,
        IConfiguration configuration,
        ILogger<ClassificationService> logger,
        ClassifyResponse stubbedResponse
    )
        : base(memoryCache, configuration, logger)
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

    private static (IMemoryCache Cache, TestableClassificationService Service) CreateService(
        ClassifyResponse? stubbedResponse = null,
        Dictionary<string, string?>? configOverrides = null
    )
    {
        var cache = new MemoryCache(new MemoryCacheOptions());

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configOverrides ?? new Dictionary<string, string?>())
            .Build();

        var logger = NullLogger<ClassificationService>.Instance;
        var response = stubbedResponse ?? new ClassifyResponse(8, "Relevant", false);
        var service = new TestableClassificationService(cache, config, logger, response);

        return (cache, service);
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
        var (cache, service) = CreateService();
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

        var cacheKey = $"clf:{TestUserId}:{contextHash}:{taskHash}";
        cache.Set(cacheKey, new ClassifyResponse(9, "Cached reason", Cached: true));

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
        // IMemoryCache evicts entries at or past their absolute expiration automatically.
        // Arrange
        var (cache, service) = CreateService();
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

        var cacheKey = $"clf:{TestUserId}:{contextHash}:{taskHash}";
        var expiredOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = DateTimeOffset.UtcNow.AddHours(-1)
        };
        cache.Set(cacheKey, new ClassifyResponse(9, "Expired", Cached: true), expiredOptions);

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
        var (_, service) = CreateService(
            stubbedResponse: new ClassifyResponse(7, "Fresh result", false)
        );

        // Act - first call hits the LLM and populates the cache
        await service.ClassifyAsync(TestUserId, DefaultRequest(), byokApiKey: "test-key");

        // Act - second call with identical inputs should be served from cache
        var second = await service.ClassifyAsync(TestUserId, DefaultRequest(), byokApiKey: "test-key");

        // Assert
        second.Cached.Should().BeTrue();
        service.LlmCallCount.Should().Be(1);
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

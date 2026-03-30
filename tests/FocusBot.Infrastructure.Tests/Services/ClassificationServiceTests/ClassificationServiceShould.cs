using System.Threading;
using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using FocusBot.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FocusBot.Infrastructure.Tests.Services.ClassificationServiceTests;

public class ClassificationServiceShould
{
    private const string ProcessName = "devenv";
    private const string WindowTitle = "FocusBot — Tests";
    private const string SessionTitle = "Write unit tests";

    private static IClassificationService BuildService(
        Mock<IAlignmentCacheRepository> cache,
        Mock<IFocusBotApiClient> apiClient,
        Mock<IClientService> clientService,
        Mock<ISettingsService> settings
    )
    {
        return new AlignmentClassificationService(
            cache.Object,
            apiClient.Object,
            clientService.Object,
            settings.Object,
            NullLogger<AlignmentClassificationService>.Instance
        );
    }

    private static Mock<IClientService> DefaultClientService()
    {
        var clientService = new Mock<IClientService>();
        clientService
            .Setup(c => c.EnsureClientIdLoadedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        clientService.Setup(c => c.GetClientId()).Returns((Guid?)null);
        return clientService;
    }

    [Fact]
    public async Task ReturnCachedResult_WhenContextAndSessionHashMatch()
    {
        // Arrange
        var cachedEntry = new AlignmentCacheEntry { Score = 9, Reason = "Cached reason" };

        var cache = new Mock<IAlignmentCacheRepository>();
        cache
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(cachedEntry);

        var apiClient = new Mock<IFocusBotApiClient>();
        apiClient.Setup(a => a.IsConfigured).Returns(true);

        var settings = new Mock<ISettingsService>();
        var clientService = DefaultClientService();
        var sut = BuildService(cache, apiClient, clientService, settings);

        // Act
        var result = await sut.ClassifyAsync(ProcessName, WindowTitle, SessionTitle, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Score.Should().Be(9);
        apiClient.Verify(
            a => a.ClassifyAsync(It.IsAny<ClassifyPayload>(), It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public async Task CallApiAndCacheResult_WhenCacheMiss()
    {
        // Arrange
        var cache = new Mock<IAlignmentCacheRepository>();
        cache
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((AlignmentCacheEntry?)null);

        var apiClient = new Mock<IFocusBotApiClient>();
        apiClient.Setup(a => a.IsConfigured).Returns(true);
        apiClient
            .Setup(a => a.ClassifyAsync(It.IsAny<ClassifyPayload>(), It.IsAny<string?>()))
            .ReturnsAsync(new ApiClassifyResponse(8, "Clearly on session", Cached: false));

        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.GetApiKeyModeAsync()).ReturnsAsync(ApiKeyMode.Managed);
        settings.Setup(s => s.GetProviderAsync()).ReturnsAsync((string?)null);
        settings.Setup(s => s.GetModelAsync()).ReturnsAsync((string?)null);

        var clientService = DefaultClientService();
        var sut = BuildService(cache, apiClient, clientService, settings);

        // Act
        var result = await sut.ClassifyAsync(ProcessName, WindowTitle, SessionTitle, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Score.Should().Be(8);
        apiClient.Verify(a => a.ClassifyAsync(It.IsAny<ClassifyPayload>(), null), Times.Once);
        cache.Verify(c => c.SaveAsync(It.Is<AlignmentCacheEntry>(e => e.Score == 8)), Times.Once);
    }

    [Fact]
    public async Task ReturnFailure_WhenNotAuthenticated()
    {
        // Arrange
        var cache = new Mock<IAlignmentCacheRepository>();
        var apiClient = new Mock<IFocusBotApiClient>();
        apiClient.Setup(a => a.IsConfigured).Returns(false);
        var settings = new Mock<ISettingsService>();
        var clientService = DefaultClientService();

        var sut = BuildService(cache, apiClient, clientService, settings);

        // Act
        var result = await sut.ClassifyAsync(ProcessName, WindowTitle, SessionTitle, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Sign in");
    }

    [Fact]
    public async Task SendByokKey_WhenApiKeyModeIsOwn()
    {
        // Arrange
        const string byokKey = "sk-test-byok-key";

        var cache = new Mock<IAlignmentCacheRepository>();
        cache
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((AlignmentCacheEntry?)null);

        var apiClient = new Mock<IFocusBotApiClient>();
        apiClient.Setup(a => a.IsConfigured).Returns(true);
        apiClient
            .Setup(a => a.ClassifyAsync(It.IsAny<ClassifyPayload>(), byokKey))
            .ReturnsAsync(new ApiClassifyResponse(7, "On session", Cached: false));

        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.GetApiKeyModeAsync()).ReturnsAsync(ApiKeyMode.Own);
        settings.Setup(s => s.GetApiKeyAsync()).ReturnsAsync(byokKey);
        settings.Setup(s => s.GetProviderAsync()).ReturnsAsync("openai");
        settings.Setup(s => s.GetModelAsync()).ReturnsAsync("gpt-4o-mini");

        var clientService = DefaultClientService();
        var sut = BuildService(cache, apiClient, clientService, settings);

        // Act
        await sut.ClassifyAsync(ProcessName, WindowTitle, SessionTitle, null);

        // Assert
        apiClient.Verify(a => a.ClassifyAsync(It.IsAny<ClassifyPayload>(), byokKey), Times.Once);
    }

    [Fact]
    public async Task NotSendByokKey_WhenApiKeyModeIsManaged()
    {
        // Arrange
        var cache = new Mock<IAlignmentCacheRepository>();
        cache
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((AlignmentCacheEntry?)null);

        var apiClient = new Mock<IFocusBotApiClient>();
        apiClient.Setup(a => a.IsConfigured).Returns(true);
        apiClient
            .Setup(a => a.ClassifyAsync(It.IsAny<ClassifyPayload>(), null))
            .ReturnsAsync(new ApiClassifyResponse(7, "On session", Cached: false));

        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.GetApiKeyModeAsync()).ReturnsAsync(ApiKeyMode.Managed);
        settings.Setup(s => s.GetProviderAsync()).ReturnsAsync((string?)null);
        settings.Setup(s => s.GetModelAsync()).ReturnsAsync((string?)null);

        var clientService = DefaultClientService();
        var sut = BuildService(cache, apiClient, clientService, settings);

        // Act
        await sut.ClassifyAsync(ProcessName, WindowTitle, SessionTitle, null);

        // Assert
        apiClient.Verify(a => a.ClassifyAsync(It.IsAny<ClassifyPayload>(), null), Times.Once);
        settings.Verify(s => s.GetApiKeyAsync(), Times.Never);
    }
}

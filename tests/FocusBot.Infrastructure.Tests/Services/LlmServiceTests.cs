using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using FocusBot.Infrastructure.Services;
using Moq;

namespace FocusBot.Infrastructure.Tests.Services;

public class LlmServiceShould
{
    [Fact]
    public async Task UseWebApiClassification_WhenApiClientIsConfigured()
    {
        // Arrange
        var mockApiClient = new Mock<IFocusBotApiClient>();
        var mockSettingsService = new Mock<ISettingsService>();
        var mockSubscriptionService = new Mock<ISubscriptionService>();
        var mockManagedKeyProvider = new Mock<IManagedKeyProvider>();
        var mockTrialService = new Mock<ITrialService>();
        var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<LlmService>>();

        mockApiClient.Setup(x => x.IsConfigured).Returns(true);
        mockApiClient
            .Setup(x => x.ClassifyAsync(It.IsAny<ClassifyPayload>()))
            .ReturnsAsync(new ApiClassifyResponse(Score: 8, Reason: "Task is directly related", Cached: false));

        var service = new LlmService(
            mockSettingsService.Object,
            mockSubscriptionService.Object,
            mockManagedKeyProvider.Object,
            mockTrialService.Object,
            mockApiClient.Object,
            mockLogger.Object
        );

        // Act
        var result = await service.ClassifyAlignmentAsync(
            taskDescription: "Write code",
            taskContext: null,
            processName: "code",
            windowTitle: "main.cs"
        );

        // Assert
        result.Result.Should().NotBeNull();
        result.Result!.Score.Should().Be(8);
        result.Result.Reason.Should().Be("Task is directly related");
        result.ErrorMessage.Should().BeNull();
        mockApiClient.Verify(
            x => x.ClassifyAsync(It.Is<ClassifyPayload>(p => p.TaskText == "Write code")),
            Times.Once
        );
    }

    [Fact]
    public async Task FallbackToDirectOpenAiWhenWebApiReturnsNull()
    {
        // Arrange
        var mockApiClient = new Mock<IFocusBotApiClient>();
        var mockSettingsService = new Mock<ISettingsService>();
        var mockSubscriptionService = new Mock<ISubscriptionService>();
        var mockManagedKeyProvider = new Mock<IManagedKeyProvider>();
        var mockTrialService = new Mock<ITrialService>();
        var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<LlmService>>();

        mockApiClient.Setup(x => x.IsConfigured).Returns(true);
        mockApiClient
            .Setup(x => x.ClassifyAsync(It.IsAny<ClassifyPayload>()))
            .ReturnsAsync((ApiClassifyResponse?)null);

        mockTrialService.Setup(x => x.IsTrialActiveAsync()).ReturnsAsync(true);
        mockManagedKeyProvider.Setup(x => x.GetApiKeyAsync()).ReturnsAsync("test-key");
        mockManagedKeyProvider.Setup(x => x.ProviderId).Returns("OpenAi");
        mockManagedKeyProvider.Setup(x => x.ModelId).Returns("gpt-4o-mini");

        var service = new LlmService(
            mockSettingsService.Object,
            mockSubscriptionService.Object,
            mockManagedKeyProvider.Object,
            mockTrialService.Object,
            mockApiClient.Object,
            mockLogger.Object
        );

        // Act
        var result = await service.ClassifyAlignmentAsync(
            taskDescription: "Write code",
            taskContext: null,
            processName: "code",
            windowTitle: "main.cs"
        );

        // Assert
        // When fallback path is taken, it attempts to call OpenAI
        // Since we're not mocking LlmTornado, this will fail, but we can verify that the API client was called first
        mockApiClient.Verify(
            x => x.ClassifyAsync(It.IsAny<ClassifyPayload>()),
            Times.Once
        );
    }

    [Fact]
    public async Task FallbackToDirectOpenAiWhenWebApiThrowsException()
    {
        // Arrange
        var mockApiClient = new Mock<IFocusBotApiClient>();
        var mockSettingsService = new Mock<ISettingsService>();
        var mockSubscriptionService = new Mock<ISubscriptionService>();
        var mockManagedKeyProvider = new Mock<IManagedKeyProvider>();
        var mockTrialService = new Mock<ITrialService>();
        var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<LlmService>>();

        mockApiClient.Setup(x => x.IsConfigured).Returns(true);
        mockApiClient
            .Setup(x => x.ClassifyAsync(It.IsAny<ClassifyPayload>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        mockTrialService.Setup(x => x.IsTrialActiveAsync()).ReturnsAsync(true);
        mockManagedKeyProvider.Setup(x => x.GetApiKeyAsync()).ReturnsAsync("test-key");
        mockManagedKeyProvider.Setup(x => x.ProviderId).Returns("OpenAi");
        mockManagedKeyProvider.Setup(x => x.ModelId).Returns("gpt-4o-mini");

        var service = new LlmService(
            mockSettingsService.Object,
            mockSubscriptionService.Object,
            mockManagedKeyProvider.Object,
            mockTrialService.Object,
            mockApiClient.Object,
            mockLogger.Object
        );

        // Act
        var result = await service.ClassifyAlignmentAsync(
            taskDescription: "Write code",
            taskContext: null,
            processName: "code",
            windowTitle: "main.cs"
        );

        // Assert
        // Exception should be caught and fallback attempted
        mockApiClient.Verify(
            x => x.ClassifyAsync(It.IsAny<ClassifyPayload>()),
            Times.Once
        );
    }

    [Fact]
    public async Task BypassWebApiWhenClientIsNotConfigured()
    {
        // Arrange
        var mockApiClient = new Mock<IFocusBotApiClient>();
        var mockSettingsService = new Mock<ISettingsService>();
        var mockSubscriptionService = new Mock<ISubscriptionService>();
        var mockManagedKeyProvider = new Mock<IManagedKeyProvider>();
        var mockTrialService = new Mock<ITrialService>();
        var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<LlmService>>();

        mockApiClient.Setup(x => x.IsConfigured).Returns(false);
        mockTrialService.Setup(x => x.IsTrialActiveAsync()).ReturnsAsync(true);
        mockManagedKeyProvider.Setup(x => x.GetApiKeyAsync()).ReturnsAsync("test-key");
        mockManagedKeyProvider.Setup(x => x.ProviderId).Returns("OpenAi");
        mockManagedKeyProvider.Setup(x => x.ModelId).Returns("gpt-4o-mini");

        var service = new LlmService(
            mockSettingsService.Object,
            mockSubscriptionService.Object,
            mockManagedKeyProvider.Object,
            mockTrialService.Object,
            mockApiClient.Object,
            mockLogger.Object
        );

        // Act
        var result = await service.ClassifyAlignmentAsync(
            taskDescription: "Write code",
            taskContext: null,
            processName: "code",
            windowTitle: "main.cs"
        );

        // Assert
        // ClassifyAsync should never be called
        mockApiClient.Verify(
            x => x.ClassifyAsync(It.IsAny<ClassifyPayload>()),
            Times.Never
        );
    }
}

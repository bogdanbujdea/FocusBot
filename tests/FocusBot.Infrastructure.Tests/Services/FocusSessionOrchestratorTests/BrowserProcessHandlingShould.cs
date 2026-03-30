using FocusBot.Core.Entities;
using FocusBot.Core.Events;
using FocusBot.Core.Interfaces;
using FocusBot.Infrastructure.Services;
using Moq;

namespace FocusBot.Infrastructure.Tests.Services.FocusSessionOrchestratorTests;

public class BrowserProcessHandlingShould
{
    private static UserSession CreateTestSession() => new()
    {
        SessionId = Guid.NewGuid().ToString(),
        SessionTitle = "Test task",
        CreatedAt = DateTime.UtcNow
    };

    [Theory]
    [InlineData("msedge")]
    [InlineData("chrome")]
    [InlineData("firefox")]
    [InlineData("brave")]
    [InlineData("opera")]
    public void SkipClassification_WhenBrowserProcessAndExtensionOnline(string processName)
    {
        // Arrange
        var windowMonitor = new Mock<IWindowMonitorService>();
        var classificationService = new Mock<IClassificationService>();
        var apiClient = new Mock<IFocusBotApiClient>();
        var sessionTracker = new Mock<ILocalSessionTracker>();
        var extensionPresence = new Mock<IExtensionPresenceService>();
        
        extensionPresence.Setup(e => e.IsExtensionOnline).Returns(true);

        var orchestrator = new FocusSessionOrchestrator(
            sessionTracker.Object,
            windowMonitor.Object,
            classificationService.Object,
            apiClient.Object,
            null,
            extensionPresence.Object
        );

        FocusSessionStateChangedEventArgs? capturedState = null;
        orchestrator.StateChanged += (sender, args) => capturedState = args;

        orchestrator.BeginLocalSessionTracking(CreateTestSession());

        // Act
        windowMonitor.Raise(m => m.ForegroundWindowChanged += null,
            new ForegroundWindowChangedEventArgs { ProcessName = processName, WindowTitle = "Some Page Title" });

        // Small delay to ensure state changed event fires
        Thread.Sleep(50);

        // Assert
        classificationService.Verify(
            c => c.ClassifyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "Should not classify browser processes when extension is online"
        );

        capturedState.Should().NotBeNull();
        capturedState!.FocusScore.Should().Be(5, "Neutral until hub updates; extension may not re-classify on focus alone");
        capturedState.FocusReason.Should().BeEmpty();
        capturedState.HasCurrentFocusResult.Should().BeTrue("Avoid infinite Checking; extension classifies on nav/title not OS focus");
        capturedState.IsClassifying.Should().BeFalse();
    }

    [Theory]
    [InlineData("msedge")]
    [InlineData("chrome")]
    public void ClassifyBrowser_WhenExtensionOffline(string processName)
    {
        // Arrange
        var windowMonitor = new Mock<IWindowMonitorService>();
        var classificationService = new Mock<IClassificationService>();
        var apiClient = new Mock<IFocusBotApiClient>();
        var sessionTracker = new Mock<ILocalSessionTracker>();
        var extensionPresence = new Mock<IExtensionPresenceService>();
        
        extensionPresence.Setup(e => e.IsExtensionOnline).Returns(false);

        var orchestrator = new FocusSessionOrchestrator(
            sessionTracker.Object,
            windowMonitor.Object,
            classificationService.Object,
            apiClient.Object,
            null,
            extensionPresence.Object
        );

        orchestrator.BeginLocalSessionTracking(CreateTestSession());

        // Act
        windowMonitor.Raise(m => m.ForegroundWindowChanged += null,
            new ForegroundWindowChangedEventArgs { ProcessName = processName, WindowTitle = "Some Page Title" });

        Thread.Sleep(50);

        // Assert
        classificationService.Verify(
            c => c.ClassifyAsync(processName, "Some Page Title", "Test task", null, It.IsAny<CancellationToken>()),
            Times.Once,
            "Should classify browser processes when extension is offline"
        );
    }

    [Fact]
    public void ClassifyBrowser_WhenNoExtensionPresenceService()
    {
        // Arrange
        var windowMonitor = new Mock<IWindowMonitorService>();
        var classificationService = new Mock<IClassificationService>();
        var apiClient = new Mock<IFocusBotApiClient>();
        var sessionTracker = new Mock<ILocalSessionTracker>();

        var orchestrator = new FocusSessionOrchestrator(
            sessionTracker.Object,
            windowMonitor.Object,
            classificationService.Object,
            apiClient.Object,
            null,
            null
        );

        orchestrator.BeginLocalSessionTracking(CreateTestSession());

        // Act
        windowMonitor.Raise(m => m.ForegroundWindowChanged += null,
            new ForegroundWindowChangedEventArgs { ProcessName = "msedge", WindowTitle = "Some Page Title" });

        Thread.Sleep(50);

        // Assert
        classificationService.Verify(
            c => c.ClassifyAsync("msedge", "Some Page Title", "Test task", null, It.IsAny<CancellationToken>()),
            Times.Once,
            "Should classify browsers when ExtensionPresenceService is null (fallback behavior)"
        );
    }

    [Fact]
    public void ClassifyNonBrowser_RegardlessOfExtensionPresence()
    {
        // Arrange
        var windowMonitor = new Mock<IWindowMonitorService>();
        var classificationService = new Mock<IClassificationService>();
        var apiClient = new Mock<IFocusBotApiClient>();
        var sessionTracker = new Mock<ILocalSessionTracker>();
        var extensionPresence = new Mock<IExtensionPresenceService>();
        
        extensionPresence.Setup(e => e.IsExtensionOnline).Returns(true);

        var orchestrator = new FocusSessionOrchestrator(
            sessionTracker.Object,
            windowMonitor.Object,
            classificationService.Object,
            apiClient.Object,
            null,
            extensionPresence.Object
        );

        orchestrator.BeginLocalSessionTracking(CreateTestSession());

        // Act
        windowMonitor.Raise(m => m.ForegroundWindowChanged += null,
            new ForegroundWindowChangedEventArgs { ProcessName = "Code", WindowTitle = "MyProject - Visual Studio Code" });

        Thread.Sleep(50);

        // Assert
        classificationService.Verify(
            c => c.ClassifyAsync("Code", "MyProject - Visual Studio Code", "Test task", null, It.IsAny<CancellationToken>()),
            Times.Once,
            "Should always classify non-browser processes, even when extension is online"
        );
    }
}

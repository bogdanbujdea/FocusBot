using CSharpFunctionalExtensions;
using FocusBot.Core.Entities;
using FocusBot.Core.Events;
using FocusBot.Core.Interfaces;
using FocusBot.Infrastructure.Services;
using Moq;

namespace FocusBot.Infrastructure.Tests.Services.FocusSessionOrchestratorTests;

/// <summary>
/// Verifies that the orchestrator correctly delegates to the hub as the single source of truth:
/// extension hub events update state, desktop hub events are ignored (the desktop already applied
/// its result locally), and the browser is skipped when the extension is online.
/// </summary>
public class HubClassificationShould
{
    private static UserSession CreateTestSession() => new()
    {
        SessionId = Guid.NewGuid().ToString(),
        SessionTitle = "Test task",
        CreatedAt = DateTime.UtcNow
    };

    private static void RaiseForeground(
        Mock<IWindowMonitorService> windowMonitor,
        string processName,
        string windowTitle)
    {
        windowMonitor.Raise(
            m => m.ForegroundWindowChanged += null,
            new ForegroundWindowChangedEventArgs { ProcessName = processName, WindowTitle = windowTitle });
    }

    [Fact]
    public void ExtensionHubEvent_UpdatesState_Immediately()
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
            extensionPresence.Object);

        FocusSessionStateChangedEventArgs? last = null;
        orchestrator.StateChanged += (_, e) => last = e;

        orchestrator.BeginLocalSessionTracking(CreateTestSession());

        // Act
        RaiseForeground(windowMonitor, "msedge", "GitHub - Pull Requests");
        orchestrator.ApplyRemoteClassificationFromHub("extension", 8, "Working on PR review", "https://github.com/pulls");

        // Assert
        last.Should().NotBeNull();
        last!.FocusScore.Should().Be(8);
        last.FocusReason.Should().Be("Working on PR review");
        last.HasCurrentFocusResult.Should().BeTrue();
        last.IsClassifying.Should().BeFalse();
    }

    [Fact]
    public void DesktopHubEvent_IsIgnored_WhenBrowserIsInForeground()
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
            extensionPresence.Object);

        FocusSessionStateChangedEventArgs? last = null;
        orchestrator.StateChanged += (_, e) => last = e;

        orchestrator.BeginLocalSessionTracking(CreateTestSession());
        RaiseForeground(windowMonitor, "msedge", "GitHub - Pull Requests");

        // Act - desktop hub event should be ignored
        orchestrator.ApplyRemoteClassificationFromHub("desktop", 7, "From desktop", "Code");

        // Assert - state stays at waiting (score=0, no result)
        last.Should().NotBeNull();
        last!.FocusScore.Should().Be(0, "Desktop hub events are ignored; desktop already applied result locally");
        last.HasCurrentFocusResult.Should().BeFalse();
    }

    [Fact]
    public void ExtensionHubEvent_UpdatesState_EvenWhenNonBrowserIsInForeground()
    {
        // Arrange
        var windowMonitor = new Mock<IWindowMonitorService>();
        var classificationService = new Mock<IClassificationService>();
        var apiClient = new Mock<IFocusBotApiClient>();
        var sessionTracker = new Mock<ILocalSessionTracker>();
        var extensionPresence = new Mock<IExtensionPresenceService>();
        extensionPresence.Setup(e => e.IsExtensionOnline).Returns(true);

        classificationService
            .Setup(c => c.ClassifyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new AlignmentResult { Score = 3, Reason = "Coding" }));

        var orchestrator = new FocusSessionOrchestrator(
            sessionTracker.Object,
            windowMonitor.Object,
            classificationService.Object,
            apiClient.Object,
            null,
            extensionPresence.Object);

        FocusSessionStateChangedEventArgs? last = null;
        orchestrator.StateChanged += (_, e) => last = e;

        orchestrator.BeginLocalSessionTracking(CreateTestSession());
        RaiseForeground(windowMonitor, "Code", "project.cs - Visual Studio Code");
        Thread.Sleep(200);

        // Act - extension hub event arrives while non-browser is in foreground
        orchestrator.ApplyRemoteClassificationFromHub("extension", 9, "Tab still aligned", "https://docs.example.com/");

        // Assert - state updates immediately with extension hub result
        last.Should().NotBeNull();
        last!.FocusScore.Should().Be(9);
        last.FocusReason.Should().Be("Tab still aligned");
        last.HasCurrentFocusResult.Should().BeTrue();
    }

    [Fact]
    public void BrowserForeground_WithExtensionOnline_SetsWaitingState_AndDoesNotClassify()
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
            extensionPresence.Object);

        FocusSessionStateChangedEventArgs? last = null;
        orchestrator.StateChanged += (_, e) => last = e;

        orchestrator.BeginLocalSessionTracking(CreateTestSession());

        // Act
        RaiseForeground(windowMonitor, "msedge", "Some Tab - Microsoft Edge");
        Thread.Sleep(50);

        // Assert - waiting state; extension will classify and broadcast via hub
        last.Should().NotBeNull();
        last!.FocusScore.Should().Be(0);
        last.FocusReason.Should().BeEmpty();
        last.HasCurrentFocusResult.Should().BeFalse();
        last.IsClassifying.Should().BeFalse();

        classificationService.Verify(
            c => c.ClassifyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void BeginLocalSessionTracking_ClearsHubClassificationState()
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
            extensionPresence.Object);

        FocusSessionStateChangedEventArgs? last = null;
        orchestrator.StateChanged += (_, e) => last = e;

        orchestrator.BeginLocalSessionTracking(CreateTestSession());
        orchestrator.ApplyRemoteClassificationFromHub("extension", 8, "Aligned", "https://z.example/");

        // Act - start a new session, state should be reset
        orchestrator.BeginLocalSessionTracking(CreateTestSession());
        RaiseForeground(windowMonitor, "msedge", "New Tab");

        // Assert - fresh waiting state
        last.Should().NotBeNull();
        last!.FocusScore.Should().Be(0);
        last.FocusReason.Should().BeEmpty();
        last.HasCurrentFocusResult.Should().BeFalse();
    }

    [Fact]
    public void StopLocalTracking_ThenRestart_StartsWithCleanState()
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
            extensionPresence.Object);

        FocusSessionStateChangedEventArgs? last = null;
        orchestrator.StateChanged += (_, e) => last = e;

        orchestrator.BeginLocalSessionTracking(CreateTestSession());
        orchestrator.ApplyRemoteClassificationFromHub("extension", 7, "Reason", "https://a.example/");

        orchestrator.StopLocalTrackingIfActive();
        orchestrator.BeginLocalSessionTracking(CreateTestSession());
        RaiseForeground(windowMonitor, "msedge", "New Tab");

        // Assert - clean state after restart
        last.Should().NotBeNull();
        last!.FocusScore.Should().Be(0);
        last.FocusReason.Should().BeEmpty();
        last.HasCurrentFocusResult.Should().BeFalse();
    }

    [Fact]
    public void ExtensionHubEvent_WhenSessionPaused_IsIgnored()
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
            extensionPresence.Object);

        FocusSessionStateChangedEventArgs? last = null;
        orchestrator.StateChanged += (_, e) => last = e;

        orchestrator.BeginLocalSessionTracking(CreateTestSession());
        orchestrator.ApplyRemotePause();

        // Act - hub event arrives while paused
        orchestrator.ApplyRemoteClassificationFromHub("extension", 9, "Aligned", "https://x.example/");

        // Assert - state stays as paused with no result
        last.Should().NotBeNull();
        last!.FocusScore.Should().Be(0, "Hub events are ignored when session is paused");
        last.HasCurrentFocusResult.Should().BeFalse();
    }
}

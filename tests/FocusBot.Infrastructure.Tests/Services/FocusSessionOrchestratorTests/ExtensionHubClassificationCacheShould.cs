using CSharpFunctionalExtensions;
using FocusBot.Core.Entities;
using FocusBot.Core.Events;
using FocusBot.Core.Interfaces;
using FocusBot.Infrastructure.Services;
using Moq;

namespace FocusBot.Infrastructure.Tests.Services.FocusSessionOrchestratorTests;

public class ExtensionHubClassificationCacheShould
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
    public void RefocusMatchesOsTitle_WhenHubActivityNameIsUrl()
    {
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

        RaiseForeground(windowMonitor, "msedge", "TeacherSeat - Materials and 13 more pages - Personal - Microsoft Edge");
        orchestrator.ApplyRemoteClassificationFromHub(
            "extension",
            7,
            "Aligned",
            "https://app.teacherseat.com/materials");

        RaiseForeground(windowMonitor, "msedge", "TeacherSeat - Materials and 13 more pages - Personal - Microsoft Edge");

        last.Should().NotBeNull();
        last!.FocusScore.Should().Be(7);
        last.FocusReason.Should().Be("Aligned");
        last.CurrentWindowTitle.Should().Contain("TeacherSeat");
    }

    [Fact]
    public void RestoresHubScoreAndReason_WhenRefocusingBrowser_AndTitleMatchesSnapshot()
    {
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

        RaiseForeground(windowMonitor, "msedge", "JSONLint - Validator");
        orchestrator.ApplyRemoteClassificationFromHub(
            "extension",
            8,
            "Aligned with task",
            "https://jsonlint.com/");

        RaiseForeground(windowMonitor, "msedge", "JSONLint - Validator");

        last.Should().NotBeNull();
        last!.FocusScore.Should().Be(8);
        last.FocusReason.Should().Be("Aligned with task");
        last.HasCurrentFocusResult.Should().BeTrue();
        classificationService.Verify(
            c => c.ClassifyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void DoesNotRestore_WhenForegroundTitleDiffersFromHubSnapshot()
    {
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

        RaiseForeground(windowMonitor, "msedge", "Tab A - Edge");
        orchestrator.ApplyRemoteClassificationFromHub("extension", 7, "Reason A", "https://a.example/");

        RaiseForeground(windowMonitor, "msedge", "Tab B - Edge");

        last.Should().NotBeNull();
        last!.FocusScore.Should().Be(5, "Different tab title → neutral until next hub classify");
        last.FocusReason.Should().BeEmpty();
        last.HasCurrentFocusResult.Should().BeTrue();
    }

    [Fact]
    public void RestoresAfterLeavingBrowser_WhenReturningToSameTitle()
    {
        var windowMonitor = new Mock<IWindowMonitorService>();
        var classificationService = new Mock<IClassificationService>();
        var apiClient = new Mock<IFocusBotApiClient>();
        var sessionTracker = new Mock<ILocalSessionTracker>();
        var extensionPresence = new Mock<IExtensionPresenceService>();
        extensionPresence.Setup(e => e.IsExtensionOnline).Returns(true);

        classificationService
            .Setup(c => c.ClassifyAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new AlignmentResult { Score = 3, Reason = "Editor" }));

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

        RaiseForeground(windowMonitor, "msedge", "Shared Title");
        orchestrator.ApplyRemoteClassificationFromHub("extension", 9, "Browser ok", "https://x.com/");

        RaiseForeground(windowMonitor, "Code", "project - Code");
        Thread.Sleep(150);

        RaiseForeground(windowMonitor, "msedge", "Shared Title");

        last.Should().NotBeNull();
        last!.FocusScore.Should().Be(9, "Cache from extension hub, not desktop classify for other app");
        last.FocusReason.Should().Be("Browser ok");
    }

    [Fact]
    public void BeginLocalSessionTracking_ClearsExtensionHubCache()
    {
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
        RaiseForeground(windowMonitor, "msedge", "Page");
        orchestrator.ApplyRemoteClassificationFromHub("extension", 8, "R", "https://z/");

        orchestrator.BeginLocalSessionTracking(CreateTestSession());
        RaiseForeground(windowMonitor, "msedge", "Page");

        last.Should().NotBeNull();
        last!.FocusScore.Should().Be(5);
        last.FocusReason.Should().BeEmpty();
    }

    [Fact]
    public void StopLocalTrackingIfActive_ClearsExtensionHubCache()
    {
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
        RaiseForeground(windowMonitor, "msedge", "Page");
        orchestrator.ApplyRemoteClassificationFromHub("extension", 8, "R", "https://z/");

        orchestrator.StopLocalTrackingIfActive();
        orchestrator.BeginLocalSessionTracking(CreateTestSession());
        RaiseForeground(windowMonitor, "msedge", "Page");

        last.Should().NotBeNull();
        last!.FocusScore.Should().Be(5);
        last.FocusReason.Should().BeEmpty();
    }

    [Fact]
    public void Restores_WhenTitleMatchesSnapshot_IgnoringCaseAndTrim()
    {
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

        RaiseForeground(windowMonitor, "msedge", "  JSONLint - Page  ");
        orchestrator.ApplyRemoteClassificationFromHub("extension", 6, "Ok", "https://j/");

        RaiseForeground(windowMonitor, "msedge", "jsonlint - page");

        last.Should().NotBeNull();
        last!.FocusScore.Should().Be(6);
        last.FocusReason.Should().Be("Ok");
    }

    [Fact]
    public void ApplyRemoteClassificationFromHub_DesktopSource_DoesNotPopulateExtensionCache()
    {
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
        RaiseForeground(windowMonitor, "msedge", "Page");

        orchestrator.ApplyRemoteClassificationFromHub("desktop", 6, "From another client", "not-used");

        RaiseForeground(windowMonitor, "msedge", "Page");

        last.Should().NotBeNull();
        last!.FocusScore.Should().Be(5, "Desktop hub is ignored; cache not filled");
        last.FocusReason.Should().BeEmpty();
    }
}

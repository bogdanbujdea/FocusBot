using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using FocusBot.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace FocusBot.Infrastructure.Tests.Services;

public class SessionCoordinatorTests
{
    [Fact]
    public async Task ApplyRemoteSessionStartedAsync_ShouldHydrateFromApi_ForDesktopSource()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var activeSession = CreateSession(sessionId);
        var apiClient = new Mock<IFocusBotApiClient>();
        apiClient.Setup(x => x.GetActiveSessionAsync()).ReturnsAsync(activeSession);
        var coordinator = CreateCoordinator(apiClient.Object);
        var evt = new SessionStartedEvent(
            sessionId,
            "Remote task",
            "context",
            DateTime.UtcNow,
            "desktop",
            Guid.NewGuid()
        );

        // Act
        await coordinator.ApplyRemoteSessionStartedAsync(evt);

        // Assert
        apiClient.Verify(x => x.GetActiveSessionAsync(), Times.Once);
        coordinator.CurrentState.ActiveSession.Should().NotBeNull();
        coordinator.CurrentState.ActiveSession!.Id.Should().Be(sessionId);
    }

    [Fact]
    public async Task ApplyRemoteSessionStartedAsync_ShouldNoOp_WhenSessionAlreadyActiveWithSameId()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var activeSession = CreateSession(sessionId);

        var apiClient = new Mock<IFocusBotApiClient>();
        apiClient.Setup(x => x.GetActiveSessionAsync()).ReturnsAsync(activeSession);

        var coordinator = CreateCoordinator(apiClient.Object);
        await coordinator.InitializeAsync();
        var evt = new SessionStartedEvent(
            sessionId,
            activeSession.SessionTitle,
            activeSession.SessionContext,
            activeSession.StartedAtUtc,
            "web",
            Guid.NewGuid()
        );

        // Act
        await coordinator.ApplyRemoteSessionStartedAsync(evt);

        // Assert
        apiClient.Verify(x => x.GetActiveSessionAsync(), Times.Once);
        coordinator.CurrentState.ActiveSession.Should().NotBeNull();
        coordinator.CurrentState.ActiveSession!.Id.Should().Be(sessionId);
    }

    [Fact]
    public async Task ApplyRemoteSessionStartedAsync_ShouldHydrateFromApi_WhenNoLocalSession()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var activeSession = CreateSession(sessionId);

        var apiClient = new Mock<IFocusBotApiClient>();
        apiClient.Setup(x => x.GetActiveSessionAsync()).ReturnsAsync(activeSession);

        var coordinator = CreateCoordinator(apiClient.Object);
        var evt = new SessionStartedEvent(
            sessionId,
            "Remote task",
            "context",
            activeSession.StartedAtUtc,
            "web",
            Guid.NewGuid()
        );

        // Act
        await coordinator.ApplyRemoteSessionStartedAsync(evt);

        // Assert
        apiClient.Verify(x => x.GetActiveSessionAsync(), Times.Once);
        coordinator.CurrentState.ActiveSession.Should().NotBeNull();
        coordinator.CurrentState.ActiveSession!.Id.Should().Be(sessionId);
        coordinator.CurrentState.LastChangeType.Should().Be(SessionChangeType.Synced);
        coordinator.CurrentState.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ApplyRemoteSessionStartedAsync_ShouldReplaceLocalSession_WhenDifferentRemoteSessionArrives()
    {
        // Arrange
        var localSessionId = Guid.NewGuid();
        var remoteSessionId = Guid.NewGuid();
        var localSession = CreateSession(localSessionId);
        var remoteSession = CreateSession(remoteSessionId);

        var apiClient = new Mock<IFocusBotApiClient>();
        apiClient
            .SetupSequence(x => x.GetActiveSessionAsync())
            .ReturnsAsync(localSession)
            .ReturnsAsync(remoteSession);

        var coordinator = CreateCoordinator(apiClient.Object);
        await coordinator.InitializeAsync();
        coordinator.CurrentState.ActiveSession!.Id.Should().Be(localSessionId);

        var evt = new SessionStartedEvent(
            remoteSessionId,
            "Remote task",
            "context",
            remoteSession.StartedAtUtc,
            "web",
            Guid.NewGuid()
        );

        // Act
        await coordinator.ApplyRemoteSessionStartedAsync(evt);

        // Assert
        apiClient.Verify(x => x.GetActiveSessionAsync(), Times.Exactly(2));
        coordinator.CurrentState.ActiveSession.Should().NotBeNull();
        coordinator.CurrentState.ActiveSession!.Id.Should().Be(remoteSessionId);
        coordinator.CurrentState.LastChangeType.Should().Be(SessionChangeType.Synced);
    }

    [Fact]
    public async Task ApplyRemoteSessionEndedAsync_ShouldClear_WhenApiReturnsNull()
    {
        var sessionId = Guid.NewGuid();
        var activeSession = CreateSession(sessionId);

        var apiClient = new Mock<IFocusBotApiClient>();
        apiClient.SetupSequence(x => x.GetActiveSessionAsync()).ReturnsAsync(activeSession).ReturnsAsync((ApiSessionResponse?)null);

        var classification = new Mock<IForegroundClassificationCoordinator>();
        var coordinator = new SessionCoordinator(
            apiClient.Object,
            classification.Object,
            Mock.Of<ILogger<SessionCoordinator>>());

        await coordinator.InitializeAsync();
        await coordinator.ApplyRemoteSessionEndedAsync(
            new SessionEndedEvent(sessionId, DateTime.UtcNow, "web"));

        coordinator.CurrentState.HasActiveSession.Should().BeFalse();
        coordinator.CurrentState.LastChangeType.Should().Be(SessionChangeType.Stopped);
        classification.Verify(x => x.Stop(), Times.Once);
    }

    [Fact]
    public async Task ApplyRemoteSessionPausedAsync_ShouldUpdate_WhenApiSessionMatchesEvent()
    {
        var sessionId = Guid.NewGuid();
        var running = CreateSession(sessionId);
        var paused = running with
        {
            IsPaused = true,
            PausedAtUtc = DateTime.UtcNow,
            TotalPausedSeconds = 0
        };

        var apiClient = new Mock<IFocusBotApiClient>();
        apiClient.SetupSequence(x => x.GetActiveSessionAsync()).ReturnsAsync(running).ReturnsAsync(paused);

        var classification = new Mock<IForegroundClassificationCoordinator>();
        var coordinator = new SessionCoordinator(
            apiClient.Object,
            classification.Object,
            Mock.Of<ILogger<SessionCoordinator>>());

        await coordinator.InitializeAsync();
        await coordinator.ApplyRemoteSessionPausedAsync(
            new SessionPausedEvent(sessionId, DateTime.UtcNow, "web"));

        coordinator.CurrentState.ActiveSession.Should().NotBeNull();
        coordinator.CurrentState.ActiveSession!.IsPaused.Should().BeTrue();
        coordinator.CurrentState.LastChangeType.Should().Be(SessionChangeType.Paused);
        classification.Verify(x => x.Stop(), Times.Once);
    }

    [Fact]
    public async Task ApplyRemoteSessionPausedAsync_ShouldNoOp_WhenApiSessionIdDiffersFromEvent()
    {
        var localId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var localSession = CreateSession(localId);

        var apiClient = new Mock<IFocusBotApiClient>();
        apiClient.Setup(x => x.GetActiveSessionAsync()).ReturnsAsync(localSession);

        var coordinator = CreateCoordinator(apiClient.Object);
        await coordinator.InitializeAsync();

        await coordinator.ApplyRemoteSessionPausedAsync(
            new SessionPausedEvent(eventId, DateTime.UtcNow, "web"));

        apiClient.Verify(x => x.GetActiveSessionAsync(), Times.Exactly(2));
        coordinator.CurrentState.ActiveSession!.Id.Should().Be(localId);
        coordinator.CurrentState.ActiveSession!.IsPaused.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyRemoteSessionResumedAsync_ShouldUpdate_WhenApiSessionMatchesEvent()
    {
        var sessionId = Guid.NewGuid();
        var paused = CreateSession(sessionId) with
        {
            IsPaused = true,
            PausedAtUtc = DateTime.UtcNow,
            TotalPausedSeconds = 60
        };
        var resumed = CreateSession(sessionId) with
        {
            IsPaused = false,
            PausedAtUtc = null,
            TotalPausedSeconds = 120
        };

        var apiClient = new Mock<IFocusBotApiClient>();
        apiClient
            .SetupSequence(x => x.GetActiveSessionAsync())
            .ReturnsAsync(paused)
            .ReturnsAsync(resumed);

        var classification = new Mock<IForegroundClassificationCoordinator>();
        var coordinator = new SessionCoordinator(
            apiClient.Object,
            classification.Object,
            Mock.Of<ILogger<SessionCoordinator>>());

        await coordinator.InitializeAsync();
        await coordinator.ApplyRemoteSessionResumedAsync(new SessionResumedEvent(sessionId, "web"));

        coordinator.CurrentState.ActiveSession.Should().NotBeNull();
        coordinator.CurrentState.ActiveSession!.IsPaused.Should().BeFalse();
        coordinator.CurrentState.LastChangeType.Should().Be(SessionChangeType.Resumed);
        classification.Verify(x => x.Start(It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
    }

    private static SessionCoordinator CreateCoordinator(IFocusBotApiClient apiClient)
    {
        return new SessionCoordinator(
            apiClient,
            Mock.Of<IForegroundClassificationCoordinator>(),
            Mock.Of<ILogger<SessionCoordinator>>());
    }

    private static ApiSessionResponse CreateSession(Guid id)
    {
        return new ApiSessionResponse(
            Id: id,
            SessionTitle: "Test task",
            SessionContext: "Test context",
            StartedAtUtc: DateTime.UtcNow,
            EndedAtUtc: null,
            PausedAtUtc: null,
            TotalPausedSeconds: 0,
            IsPaused: false,
            Source: "web"
        );
    }
}

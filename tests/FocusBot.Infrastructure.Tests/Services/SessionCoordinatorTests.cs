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

    private static SessionCoordinator CreateCoordinator(IFocusBotApiClient apiClient)
    {
        return new SessionCoordinator(apiClient, Mock.Of<ILogger<SessionCoordinator>>());
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

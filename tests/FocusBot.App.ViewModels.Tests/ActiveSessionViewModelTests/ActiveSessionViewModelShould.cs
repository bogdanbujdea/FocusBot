using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using Moq;

namespace FocusBot.App.ViewModels.Tests.ActiveSessionViewModelTests;

public class ActiveSessionViewModelShould
{
    private static ApiSessionResponse CreateTestSession(Guid? id = null, bool isPaused = false) => new(
        Id: id ?? Guid.NewGuid(),
        SessionTitle: "Test Task",
        SessionContext: "Test context",
        StartedAtUtc: DateTime.UtcNow.AddMinutes(-5),
        EndedAtUtc: null,
        PausedAtUtc: isPaused ? DateTime.UtcNow.AddMinutes(-1) : null,
        TotalPausedSeconds: isPaused ? 60 : 0,
        IsPaused: isPaused,
        Source: "desktop"
    );

    [Fact]
    public async Task CallCoordinator_WhenStopExecuted()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var testSession = CreateTestSession(id: sessionId);
        
        var mockCoordinator = new Mock<ISessionCoordinator>();
        mockCoordinator.Setup(x => x.StopAsync()).ReturnsAsync(true);

        var mockDispatcher = new Mock<IUIThreadDispatcher>();
        mockDispatcher.Setup(x => x.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(f => f());

        var mockClassificationCoordinator = new Mock<IForegroundClassificationCoordinator>();
        var vm = new ActiveSessionViewModel(mockDispatcher.Object, mockCoordinator.Object, mockClassificationCoordinator.Object);
        await vm.LoadAsync(testSession);

        // Act
        await vm.StopCommand.ExecuteAsync(null);

        // Assert
        mockCoordinator.Verify(x => x.StopAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateIsPaused_WhenPauseCommandExecuted()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var initialSession = CreateTestSession(id: sessionId, isPaused: false);
        var pausedSession = new ApiSessionResponse(
            Id: sessionId,
            SessionTitle: initialSession.SessionTitle,
            SessionContext: initialSession.SessionContext,
            StartedAtUtc: initialSession.StartedAtUtc,
            EndedAtUtc: null,
            PausedAtUtc: DateTime.UtcNow,
            TotalPausedSeconds: 0,
            IsPaused: true,
            Source: "desktop"
        );

        var mockCoordinator = new Mock<ISessionCoordinator>();
        mockCoordinator.Setup(x => x.PauseAsync())
            .ReturnsAsync(true)
            .Callback(() =>
            {
                var state = new SessionState(pausedSession, null, SessionChangeType.Paused);
                mockCoordinator.Raise(m => m.StateChanged += null, state, SessionChangeType.Paused);
            });

        var mockDispatcher = new Mock<IUIThreadDispatcher>();
        mockDispatcher.Setup(x => x.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(f => f());

        var mockClassificationCoordinator = new Mock<IForegroundClassificationCoordinator>();
        var vm = new ActiveSessionViewModel(mockDispatcher.Object, mockCoordinator.Object, mockClassificationCoordinator.Object);
        await vm.LoadAsync(initialSession);

        vm.IsPaused.Should().BeFalse();

        // Act
        await vm.PauseOrResumeCommand.ExecuteAsync(null);

        // Assert
        mockCoordinator.Verify(x => x.PauseAsync(), Times.Once);
        vm.IsPaused.Should().BeTrue();
    }

    [Fact]
    public async Task ShowError_WhenStopFails()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var testSession = CreateTestSession(id: sessionId);

        var mockCoordinator = new Mock<ISessionCoordinator>();
        mockCoordinator.Setup(x => x.StopAsync())
            .ReturnsAsync(false)
            .Callback(() =>
            {
                var state = new SessionState(testSession, "Failed to stop", SessionChangeType.Failed);
                mockCoordinator.Raise(m => m.StateChanged += null, state, SessionChangeType.Failed);
            });

        var mockDispatcher = new Mock<IUIThreadDispatcher>();
        mockDispatcher.Setup(x => x.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(f => f());

        var mockClassificationCoordinator = new Mock<IForegroundClassificationCoordinator>();
        var vm = new ActiveSessionViewModel(mockDispatcher.Object, mockCoordinator.Object, mockClassificationCoordinator.Object);
        await vm.LoadAsync(testSession);

        vm.State.ErrorMessage.Should().BeNull();

        // Act
        await vm.StopCommand.ExecuteAsync(null);

        // Assert
        vm.State.ErrorMessage.Should().NotBeNullOrEmpty();
        vm.State.IsBusy.Should().BeFalse();
    }
}

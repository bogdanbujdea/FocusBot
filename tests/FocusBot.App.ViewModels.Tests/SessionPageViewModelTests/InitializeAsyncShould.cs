using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using Moq;

namespace FocusBot.App.ViewModels.Tests.SessionPageViewModelTests;

public class InitializeAsyncShould
{
    private static ApiSessionResponse CreateTestSession() => new(
        Id: Guid.NewGuid(),
        SessionTitle: "Test Task",
        SessionContext: "Test context",
        StartedAtUtc: DateTime.UtcNow,
        EndedAtUtc: null,
        PausedAtUtc: null,
        TotalPausedSeconds: 0,
        IsPaused: false,
        Source: "desktop"
    );

    [Fact]
    public async Task SetHasActiveSession_When_CoordinatorReturnsSession()
    {
        // Arrange
        var testSession = CreateTestSession();
        var mockCoordinator = new Mock<ISessionCoordinator>();
        mockCoordinator.Setup(x => x.InitializeAsync())
            .Returns(Task.CompletedTask)
            .Callback(() =>
            {
                var state = new SessionState(testSession, null, SessionChangeType.Synced);
                mockCoordinator.Raise(m => m.StateChanged += null, state, SessionChangeType.Synced);
            });

        var mockNavigation = new Mock<INavigationService>();
        var mockDispatcher = new Mock<IUIThreadDispatcher>();
        mockDispatcher.Setup(x => x.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(f => f());

        var newSessionVm = new NewSessionViewModel(mockCoordinator.Object);
        var vm = new SessionPageViewModel(
            newSessionVm,
            mockNavigation.Object,
            mockCoordinator.Object,
            mockDispatcher.Object);

        // Act
        await vm.InitializeAsync();

        // Assert
        vm.HasActiveSession.Should().BeTrue();
        vm.ActiveSession.Should().NotBeNull();
        vm.ActiveSession!.SessionTitle.Should().Be(testSession.SessionTitle);
    }

    [Fact]
    public async Task NotSetActiveSession_When_CoordinatorReturnsNull()
    {
        // Arrange
        var mockCoordinator = new Mock<ISessionCoordinator>();
        mockCoordinator.Setup(x => x.InitializeAsync())
            .Returns(Task.CompletedTask)
            .Callback(() =>
            {
                var state = new SessionState(null, null, SessionChangeType.Started);
                mockCoordinator.Raise(m => m.StateChanged += null, state, SessionChangeType.Started);
            });

        var mockNavigation = new Mock<INavigationService>();
        var mockDispatcher = new Mock<IUIThreadDispatcher>();
        mockDispatcher.Setup(x => x.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(f => f());

        var newSessionVm = new NewSessionViewModel(mockCoordinator.Object);
        var vm = new SessionPageViewModel(
            newSessionVm,
            mockNavigation.Object,
            mockCoordinator.Object,
            mockDispatcher.Object);

        // Act
        await vm.InitializeAsync();

        // Assert
        vm.HasActiveSession.Should().BeFalse();
        vm.ActiveSession.Should().BeNull();
    }
}

using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using Moq;

namespace FocusBot.App.ViewModels.Tests.NewSessionViewModelTests;

public class StartAsyncShould
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
    public async Task InvokeCoordinator()
    {
        // Arrange
        var mockCoordinator = new Mock<ISessionCoordinator>();
        mockCoordinator.Setup(x => x.StartAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var vm = new NewSessionViewModel(mockCoordinator.Object)
        {
            SessionTitle = "Test Task",
            SessionContext = "Test context"
        };

        // Act
        await vm.StartCommand.ExecuteAsync(null);

        // Assert
        mockCoordinator.Verify(x => x.StartAsync("Test Task", "Test context"), Times.Once);
    }

    [Fact]
    public async Task UpdateUI_When_ResultIsSuccessful()
    {
        // Arrange
        var testSession = CreateTestSession();
        var mockCoordinator = new Mock<ISessionCoordinator>();
        mockCoordinator.Setup(x => x.StartAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true)
            .Callback(() =>
            {
                var state = new SessionState(testSession, null, SessionChangeType.Started);
                mockCoordinator.Raise(m => m.StateChanged += null, state, SessionChangeType.Started);
            });

        var vm = new NewSessionViewModel(mockCoordinator.Object)
        {
            SessionTitle = "Test Task",
            SessionContext = "Test context"
        };

        // Act
        await vm.StartCommand.ExecuteAsync(null);

        // Assert
        vm.SessionTitle.Should().BeEmpty();
        vm.SessionContext.Should().BeEmpty();
        vm.State.IsBusy.Should().BeFalse();
        vm.State.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ShowError_When_ErrorReturned()
    {
        // Arrange
        var mockCoordinator = new Mock<ISessionCoordinator>();
        mockCoordinator.Setup(x => x.StartAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false)
            .Callback(() =>
            {
                var state = new SessionState(null, "Failed to start", SessionChangeType.Failed);
                mockCoordinator.Raise(m => m.StateChanged += null, state, SessionChangeType.Failed);
            });

        var vm = new NewSessionViewModel(mockCoordinator.Object)
        {
            SessionTitle = "Test Task"
        };

        // Act
        await vm.StartCommand.ExecuteAsync(null);

        // Assert
        vm.State.ErrorMessage.Should().NotBeNullOrEmpty();
        vm.State.IsBusy.Should().BeFalse();
        vm.SessionTitle.Should().Be("Test Task");
    }

    [Fact]
    public async Task ClearError_When_ClearErrorCommandExecuted()
    {
        // Arrange
        var mockCoordinator = new Mock<ISessionCoordinator>();
        mockCoordinator.Setup(x => x.StartAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false)
            .Callback(() =>
            {
                var state = new SessionState(null, "Failed to start", SessionChangeType.Failed);
                mockCoordinator.Raise(m => m.StateChanged += null, state, SessionChangeType.Failed);
            });

        var vm = new NewSessionViewModel(mockCoordinator.Object)
        {
            SessionTitle = "Test Task"
        };

        await vm.StartCommand.ExecuteAsync(null);
        vm.State.ErrorMessage.Should().NotBeNullOrEmpty();

        // Act
        vm.ClearErrorCommand.Execute(null);

        // Assert
        mockCoordinator.Verify(x => x.ClearError(), Times.Once);
    }
}

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
    public async Task InvokeStopCommand_WhenSessionEnded()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var testSession = CreateTestSession(id: sessionId);
        
        var mockService = new Mock<IFocusSessionControlService>();
        mockService.Setup(x => x.EndWithPlaceholderMetricsAsync(sessionId))
            .ReturnsAsync(ApiResult<ApiSessionResponse>.Success(testSession));

        var mockDispatcher = new Mock<IUIThreadDispatcher>();
        var vm = new ActiveSessionViewModel(mockDispatcher.Object, mockService.Object);
        vm.SetSession(testSession);

        bool onSessionEndedCalled = false;
        vm.OnSessionEnded = () => onSessionEndedCalled = true;

        // Act
        await vm.StopCommand.ExecuteAsync(null);

        // Assert
        mockService.Verify(x => x.EndWithPlaceholderMetricsAsync(sessionId), Times.Once);
        onSessionEndedCalled.Should().BeTrue();
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

        var mockService = new Mock<IFocusSessionControlService>();
        mockService.Setup(x => x.TogglePauseAsync(sessionId, false))
            .ReturnsAsync(ApiResult<ApiSessionResponse>.Success(pausedSession));

        var mockDispatcher = new Mock<IUIThreadDispatcher>();
        var vm = new ActiveSessionViewModel(mockDispatcher.Object, mockService.Object);
        vm.SetSession(initialSession);

        vm.IsPaused.Should().BeFalse();

        // Act
        await vm.PauseOrResumeCommand.ExecuteAsync(null);

        // Assert
        mockService.Verify(x => x.TogglePauseAsync(sessionId, false), Times.Once);
        vm.IsPaused.Should().BeTrue();
    }

    [Fact]
    public async Task ShowError_WhenStopFails()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var testSession = CreateTestSession(id: sessionId);

        var mockService = new Mock<IFocusSessionControlService>();
        mockService.Setup(x => x.EndWithPlaceholderMetricsAsync(sessionId))
            .ReturnsAsync(ApiResult<ApiSessionResponse>.Failure(System.Net.HttpStatusCode.InternalServerError));

        var mockDispatcher = new Mock<IUIThreadDispatcher>();
        var vm = new ActiveSessionViewModel(mockDispatcher.Object, mockService.Object);
        vm.SetSession(testSession);

        vm.State.ErrorMessage.Should().BeNull();

        // Act
        await vm.StopCommand.ExecuteAsync(null);

        // Assert
        vm.State.ErrorMessage.Should().NotBeNullOrEmpty();
        vm.State.IsBusy.Should().BeFalse();
    }
}

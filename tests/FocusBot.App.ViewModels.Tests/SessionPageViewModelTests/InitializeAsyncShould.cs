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
    public async Task SetHasActiveSession_When_ApiReturnsSession()
    {
        // Arrange
        var testSession = CreateTestSession();
        var mockApi = new Mock<IFocusBotApiClient>();
        mockApi.Setup(x => x.GetActiveSessionAsync())
            .ReturnsAsync(testSession);

        var mockNavigation = new Mock<INavigationService>();
        var mockDispatcher = new Mock<IUIThreadDispatcher>();
        mockDispatcher.Setup(x => x.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(f => f());
        
        var mockSessionControl = new Mock<IFocusSessionControlService>();

        var newSessionVm = new NewSessionViewModel(mockApi.Object);
        var vm = new SessionPageViewModel(
            newSessionVm,
            mockNavigation.Object,
            mockApi.Object,
            mockSessionControl.Object,
            mockDispatcher.Object);

        // Act
        await vm.InitializeAsync();

        // Assert
        vm.HasActiveSession.Should().BeTrue();
        vm.ActiveSession.Should().NotBeNull();
        vm.ActiveSession!.SessionTitle.Should().Be(testSession.SessionTitle);
    }

    [Fact]
    public async Task NotSetActiveSession_When_ApiReturnsNull()
    {
        // Arrange
        var mockApi = new Mock<IFocusBotApiClient>();
        mockApi.Setup(x => x.GetActiveSessionAsync())
            .ReturnsAsync((ApiSessionResponse?)null);

        var mockNavigation = new Mock<INavigationService>();
        var mockDispatcher = new Mock<IUIThreadDispatcher>();
        var mockSessionControl = new Mock<IFocusSessionControlService>();

        var newSessionVm = new NewSessionViewModel(mockApi.Object);
        var vm = new SessionPageViewModel(
            newSessionVm,
            mockNavigation.Object,
            mockApi.Object,
            mockSessionControl.Object,
            mockDispatcher.Object);

        // Act
        await vm.InitializeAsync();

        // Assert
        vm.HasActiveSession.Should().BeFalse();
        vm.ActiveSession.Should().BeNull();
    }
}

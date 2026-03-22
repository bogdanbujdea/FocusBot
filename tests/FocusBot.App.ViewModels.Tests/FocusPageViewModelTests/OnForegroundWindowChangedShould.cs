using FocusBot.Core.Entities;
using FocusBot.Core.Events;
using FocusBot.Core.Interfaces;
using Moq;

namespace FocusBot.App.ViewModels.Tests.FocusPageViewModelTests;

public class OnForegroundWindowChangedShould
{
    [Fact]
    public async Task SetProcessNameAndWindowTitle_WhenOrchestratorStateChanges()
    {
        // Arrange
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        orchestratorMock.Setup(o => o.LoadActiveSessionAsync()).ReturnsAsync((UserSession?)null);
        var navMock = new Mock<INavigationService>();
        var settingsMock = new Mock<ISettingsService>();
        var accountVm = new AccountSettingsViewModel(
            Mock.Of<IAuthService>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<AccountSettingsViewModel>>());
        var statusBar = new FocusStatusViewModel(orchestratorMock.Object);
        var vm = new FocusPageViewModel(
            navMock.Object,
            settingsMock.Object,
            orchestratorMock.Object,
            accountVm,
            statusBar);

        var stateArgs = new FocusSessionStateChangedEventArgs
        {
            SessionElapsedSeconds = 0,
            FocusScorePercent = 0,
            IsClassifying = false,
            FocusScore = 0,
            FocusReason = string.Empty,
            HasCurrentFocusResult = false,
            IsSessionPaused = false,
            CurrentProcessName = "devenv",
            CurrentWindowTitle = "MyFile.cs",
        };

        // Act
        orchestratorMock.Raise(m => m.StateChanged += null, orchestratorMock.Object, stateArgs);

        // Assert
        vm.Status.CurrentProcessName.Should().Be("devenv");
        vm.Status.CurrentWindowTitle.Should().Be("MyFile.cs");
    }
}

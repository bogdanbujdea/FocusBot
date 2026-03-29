using FocusBot.Core.Entities;
using FocusBot.Core.Events;
using FocusBot.Core.Interfaces;
using Moq;

namespace FocusBot.App.ViewModels.Tests.FocusPageViewModelTests;

public class TaskElapsedTimeShould
{
    [Fact]
    public async Task UpdateFromOrchestratorStateChange()
    {
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
            Mock.Of<IFocusHubClient>(),
            new StubPlanService(),
            accountVm,
            statusBar);

        await Task.Delay(150);

        orchestratorMock.Raise(m => m.StateChanged += null, orchestratorMock.Object, CreateStateArgs(sessionElapsedSeconds: 1));

        vm.SessionElapsedTime.Should().Be("00:00:01");

        orchestratorMock.Raise(m => m.StateChanged += null, orchestratorMock.Object, CreateStateArgs(sessionElapsedSeconds: 2));

        vm.SessionElapsedTime.Should().Be("00:00:02");
    }

    [Fact]
    public async Task ShowFormattedTime_FromOrchestratorState()
    {
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
            Mock.Of<IFocusHubClient>(),
            new StubPlanService(),
            accountVm,
            statusBar);

        await Task.Delay(150);

        orchestratorMock.Raise(m => m.StateChanged += null, orchestratorMock.Object, CreateStateArgs(sessionElapsedSeconds: 3661));

        vm.SessionElapsedTime.Should().Be("01:01:01");
    }

    [Fact]
    public async Task ShowZero_WhenNoSessionActive()
    {
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
            Mock.Of<IFocusHubClient>(),
            new StubPlanService(),
            accountVm,
            statusBar);
        await Task.Delay(150);

        vm.SessionElapsedTime.Should().Be("00:00:00");
    }

    private static FocusSessionStateChangedEventArgs CreateStateArgs(
        long sessionElapsedSeconds = 0,
        int focusScorePercent = 0,
        bool isClassifying = false,
        int focusScore = 0,
        string focusReason = "",
        bool hasCurrentFocusResult = false,
        bool isSessionPaused = false,
        string currentProcessName = "",
        string currentWindowTitle = "")
    {
        return new FocusSessionStateChangedEventArgs
        {
            SessionElapsedSeconds = sessionElapsedSeconds,
            FocusScorePercent = focusScorePercent,
            IsClassifying = isClassifying,
            FocusScore = focusScore,
            FocusReason = focusReason,
            HasCurrentFocusResult = hasCurrentFocusResult,
            IsSessionPaused = isSessionPaused,
            CurrentProcessName = currentProcessName,
            CurrentWindowTitle = currentWindowTitle,
        };
    }
}

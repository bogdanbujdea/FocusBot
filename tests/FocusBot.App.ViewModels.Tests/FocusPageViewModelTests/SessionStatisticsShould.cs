using FocusBot.Core.Entities;
using FocusBot.Core.Events;
using FocusBot.Core.Interfaces;
using Moq;

namespace FocusBot.App.ViewModels.Tests.FocusPageViewModelTests;

public class SessionStatisticsShould
{
    [Fact]
    public async Task UpdateFocusedDistractedAndDistractionCount_FromOrchestratorState()
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
            accountVm,
            statusBar);

        await Task.Delay(150);

        orchestratorMock.Raise(
            m => m.StateChanged += null,
            orchestratorMock.Object,
            CreateStateArgs(
                sessionElapsedSeconds: 120,
                focusScorePercent: 75,
                focusedSeconds: 90,
                distractedSeconds: 30,
                distractionCount: 3));

        vm.SessionElapsedTime.Should().Be("00:02:00");
        vm.FocusedTime.Should().Be("00:01:30");
        vm.DistractedTime.Should().Be("00:00:30");
        vm.DistractionCount.Should().Be(3);
        vm.CurrentFocusScorePercent.Should().Be(75);
        vm.DistractedBarStarWeight.Should().Be(25);
        vm.FocusedPercentLabel.Should().Be("75% Focused");
        vm.DistractedPercentLabel.Should().Be("25% Distracted");
    }

    private static FocusSessionStateChangedEventArgs CreateStateArgs(
        long sessionElapsedSeconds = 0,
        int focusScorePercent = 0,
        long focusedSeconds = 0,
        long distractedSeconds = 0,
        int distractionCount = 0,
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
            FocusedSeconds = focusedSeconds,
            DistractedSeconds = distractedSeconds,
            DistractionCount = distractionCount,
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

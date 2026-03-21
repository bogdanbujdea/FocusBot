using FocusBot.Core.Events;
using FocusBot.Core.Interfaces;
using Moq;

namespace FocusBot.App.ViewModels.Tests.FocusPageViewModelTests;

public class TaskElapsedTimeShould
{
    [Fact]
    public async Task UpdateFromOrchestratorStateChange()
    {
        // Arrange
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var task = await ctx.Repo.AddSessionAsync("Tracked task");
        await ctx.Repo.SetActiveAsync(task.SessionId);

        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        var navMock = new Mock<INavigationService>();
        var settingsMock = new Mock<ISettingsService>();
        var accountVm = new AccountSettingsViewModel(
            Mock.Of<IAuthService>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<AccountSettingsViewModel>>());
        var vm = new FocusPageViewModel(
            ctx.Repo,
            navMock.Object,
            settingsMock.Object,
            orchestratorMock.Object,
            accountVm);

        // Act - first tick
        orchestratorMock.Raise(m => m.StateChanged += null, orchestratorMock.Object, CreateStateArgs(sessionElapsedSeconds: 1));

        // Assert
        vm.SessionElapsedTime.Should().Be("00:00:01");

        // Act - second tick
        orchestratorMock.Raise(m => m.StateChanged += null, orchestratorMock.Object, CreateStateArgs(sessionElapsedSeconds: 2));

        // Assert
        vm.SessionElapsedTime.Should().Be("00:00:02");
    }

    [Fact]
    public async Task ShowFormattedTime_FromOrchestratorState()
    {
        // Arrange
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var task = await ctx.Repo.AddSessionAsync("Resumed task");
        await ctx.Repo.SetActiveAsync(task.SessionId);

        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        var navMock = new Mock<INavigationService>();
        var settingsMock = new Mock<ISettingsService>();
        var accountVm = new AccountSettingsViewModel(
            Mock.Of<IAuthService>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<AccountSettingsViewModel>>());
        var vm = new FocusPageViewModel(
            ctx.Repo,
            navMock.Object,
            settingsMock.Object,
            orchestratorMock.Object,
            accountVm);

        // Act - simulate orchestrator state with 1h 1m 1s
        orchestratorMock.Raise(m => m.StateChanged += null, orchestratorMock.Object, CreateStateArgs(sessionElapsedSeconds: 3661));

        // Assert
        vm.SessionElapsedTime.Should().Be("01:01:01");
    }

    [Fact]
    public async Task ShowZero_WhenNoSessionActive()
    {
        // Arrange
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        var navMock = new Mock<INavigationService>();
        var settingsMock = new Mock<ISettingsService>();
        var accountVm = new AccountSettingsViewModel(
            Mock.Of<IAuthService>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<AccountSettingsViewModel>>());
        var vm = new FocusPageViewModel(
            ctx.Repo,
            navMock.Object,
            settingsMock.Object,
            orchestratorMock.Object,
            accountVm);
        await Task.Delay(150);

        // Assert - no state change, so elapsed time should be 00:00:00
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

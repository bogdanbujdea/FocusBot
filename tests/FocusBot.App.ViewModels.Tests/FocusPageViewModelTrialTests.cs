using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using FocusBot.App.ViewModels.Tests.FocusPageViewModelTests;
using Moq;

namespace FocusBot.App.ViewModels.Tests;

public class FocusPageViewModelTrialTests
{
    [Fact]
    public async Task IsTrialBannerVisible_True_WhenFoqusTrialActive()
    {
        var planSvc = new StubPlanService
        {
            CurrentPlan = ClientPlanType.FreeBYOK,
            Status = ClientSubscriptionStatus.Trial,
            TrialEndsAtUtc = DateTime.UtcNow.AddHours(5),
        };

        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        orchestratorMock.Setup(o => o.LoadActiveSessionAsync()).ReturnsAsync((UserSession?)null);

        var authMock = new Mock<IAuthService>();
        authMock.Setup(a => a.IsAuthenticated).Returns(true);

        var settingsMock = new Mock<ISettingsService>();
        var accountVm = new AccountSettingsViewModel(
            authMock.Object,
            Mock.Of<Microsoft.Extensions.Logging.ILogger<AccountSettingsViewModel>>());

        var statusBar = new FocusStatusViewModel(orchestratorMock.Object);
        var vm = new FocusPageViewModel(
            Mock.Of<INavigationService>(),
            settingsMock.Object,
            orchestratorMock.Object,
            Mock.Of<IFocusHubClient>(),
            planSvc,
            accountVm,
            statusBar);

        await Task.Delay(400);

        vm.IsTrialBannerVisible.Should().BeTrue();
        vm.TrialCountdownMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ShowBYOKKeyPromptRequested_Raises_WhenCloudBYOKAndNoApiKey()
    {
        var planSvc = new StubPlanService
        {
            CurrentPlan = ClientPlanType.CloudBYOK,
            Status = ClientSubscriptionStatus.Active,
            TrialEndsAtUtc = null,
        };

        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        orchestratorMock.Setup(o => o.LoadActiveSessionAsync()).ReturnsAsync((UserSession?)null);

        var authMock = new Mock<IAuthService>();
        authMock.Setup(a => a.IsAuthenticated).Returns(true);

        var settingsMock = new Mock<ISettingsService>();
        settingsMock.Setup(s => s.GetApiKeyAsync()).ReturnsAsync((string?)null);

        var accountVm = new AccountSettingsViewModel(
            authMock.Object,
            Mock.Of<Microsoft.Extensions.Logging.ILogger<AccountSettingsViewModel>>());

        var statusBar = new FocusStatusViewModel(orchestratorMock.Object);
        var vm = new FocusPageViewModel(
            Mock.Of<INavigationService>(),
            settingsMock.Object,
            orchestratorMock.Object,
            Mock.Of<IFocusHubClient>(),
            planSvc,
            accountVm,
            statusBar);

        var raised = false;
        vm.ShowBYOKKeyPromptRequested += (_, _) => raised = true;

        await Task.Delay(200);

        planSvc.RaisePlanChanged(ClientPlanType.CloudBYOK);

        await Task.Delay(200);

        raised.Should().BeTrue();
    }
}

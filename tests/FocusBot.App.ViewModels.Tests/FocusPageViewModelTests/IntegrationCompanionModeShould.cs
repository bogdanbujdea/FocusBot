using FocusBot.Core.DTOs;
using FocusBot.Core.Interfaces;
using Moq;

namespace FocusBot.App.ViewModels.Tests.FocusPageViewModelTests;

public class IntegrationCompanionModeShould
{
    [Fact]
    public async Task SetRemoteTaskAndMonitoring_When_TaskStartedReceived_From_Extension()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var monitorMock = new Mock<IWindowMonitorService>();
        var integrationMock = new Mock<IIntegrationService>();
        var vm = CreateViewModel(ctx, monitorMock, integrationMock.Object, uiDispatcher: null);

        var payload = new TaskStartedPayload
        {
            TaskId = "ext-session-1",
            TaskText = "Watch a movie",
            TaskHints = null
        };

        integrationMock.Raise(m => m.TaskStartedReceived += null, integrationMock.Object, payload);

        vm.RemoteTaskFromExtension.Should().NotBeNull();
        vm.RemoteTaskFromExtension!.TaskId.Should().Be("ext-session-1");
        vm.RemoteTaskFromExtension.TaskText.Should().Be("Watch a movie");
        vm.IsMonitoring.Should().BeTrue();
    }

    [Fact]
    public async Task StartWindowMonitor_When_TaskStartedReceived_From_Extension()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var monitorMock = new Mock<IWindowMonitorService>();
        var integrationMock = new Mock<IIntegrationService>();
        var vm = CreateViewModel(ctx, monitorMock, integrationMock.Object, uiDispatcher: null);

        var payload = new TaskStartedPayload
        {
            TaskId = "ext-1",
            TaskText = "Code review",
            TaskHints = null
        };

        integrationMock.Raise(m => m.TaskStartedReceived += null, integrationMock.Object, payload);

        monitorMock.Verify(m => m.Start(), Times.Once);
    }

    [Fact]
    public async Task StopWindowMonitor_When_TaskEndedReceived_And_No_Local_InProgress_Task()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var monitorMock = new Mock<IWindowMonitorService>();
        var integrationMock = new Mock<IIntegrationService>();

        var vm = CreateViewModel(ctx, monitorMock, integrationMock.Object, uiDispatcher: null);

        var payload = new TaskStartedPayload { TaskId = "ext-1", TaskText = "Task", TaskHints = null };
        integrationMock.Raise(m => m.TaskStartedReceived += null, integrationMock.Object, payload);
        monitorMock.Reset();

        integrationMock.Raise(m => m.TaskEndedReceived += null, integrationMock.Object, EventArgs.Empty);

        monitorMock.Verify(m => m.Stop(), Times.Once);
    }

    [Fact]
    public async Task Not_StopWindowMonitor_When_TaskEndedReceived_And_Local_InProgress_Task_Exists()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var task = await ctx.Repo.AddTaskAsync("Local task");
        await ctx.Repo.SetActiveAsync(task.TaskId);

        var monitorMock = new Mock<IWindowMonitorService>();
        var integrationMock = new Mock<IIntegrationService>();

        var vm = CreateViewModel(ctx, monitorMock, integrationMock.Object, uiDispatcher: null);
        await Task.Delay(300);

        monitorMock.Reset();

        integrationMock.Raise(m => m.TaskEndedReceived += null, integrationMock.Object, EventArgs.Empty);

        monitorMock.Verify(m => m.Stop(), Times.Never);
    }

    private static FocusPageViewModel CreateViewModel(
        FocusPageTestContext ctx,
        Mock<IWindowMonitorService> monitorMock,
        IIntegrationService integrationService,
        IUIThreadDispatcher? uiDispatcher)
    {
        var navMock = new Mock<INavigationService>();
        var settingsMock = new Mock<ISettingsService>();
        var accountVm = new AccountSettingsViewModel(
            Mock.Of<IAuthService>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<AccountSettingsViewModel>>());

        return new FocusPageViewModel(
            ctx.Repo,
            monitorMock.Object,
            navMock.Object,
            Mock.Of<IClassificationService>(),
            settingsMock.Object,
            Mock.Of<ILocalSessionTracker>(),
            Mock.Of<IAlignmentCacheRepository>(),
            Mock.Of<IFocusBotApiClient>(),
            accountVm,
            integrationService,
            uiDispatcher);
    }
}

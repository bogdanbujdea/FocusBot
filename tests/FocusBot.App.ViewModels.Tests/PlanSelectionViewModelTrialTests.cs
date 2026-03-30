using FocusBot.Core.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FocusBot.App.ViewModels.Tests;

public class PlanSelectionViewModelTrialTests
{
    private sealed class TestPlanService : IPlanService
    {
        public ClientPlanType Plan { get; set; } = ClientPlanType.FreeBYOK;
        public ClientSubscriptionStatus ClientStatus { get; set; } = ClientSubscriptionStatus.Trial;
        public DateTime? TrialEndsAtUtc { get; set; } = DateTime.UtcNow.AddHours(24);
        public DateTime? CurrentPeriodEndsAtUtc { get; set; }

#pragma warning disable CS0067
        public event EventHandler<ClientPlanType>? PlanChanged;
#pragma warning restore CS0067

        public Task<ClientPlanType> GetCurrentPlanAsync(CancellationToken ct = default) =>
            Task.FromResult(Plan);

        public Task RefreshAsync(CancellationToken ct = default) => Task.CompletedTask;

        public bool IsCloudPlan(ClientPlanType plan) =>
            plan is ClientPlanType.CloudBYOK or ClientPlanType.CloudManaged;

        public Task<ClientSubscriptionStatus> GetStatusAsync(CancellationToken ct = default) =>
            Task.FromResult(ClientStatus);

        public Task<DateTime?> GetTrialEndsAtAsync(CancellationToken ct = default) =>
            Task.FromResult(TrialEndsAtUtc);

        public Task<DateTime?> GetCurrentPeriodEndsAtAsync(CancellationToken ct = default) =>
            Task.FromResult(CurrentPeriodEndsAtUtc);
    }

    [Fact]
    public async Task ShowUpgradeCta_True_WhenTrialEndedAndNotOnCloudPlan()
    {
        var planSvc = new TestPlanService
        {
            Plan = ClientPlanType.FreeBYOK,
            ClientStatus = ClientSubscriptionStatus.Trial,
            TrialEndsAtUtc = DateTime.UtcNow.AddHours(-1),
        };

        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.IsAuthenticated).Returns(true);

        var vm = new PlanSelectionViewModel(planSvc, auth.Object, NullLogger<PlanSelectionViewModel>.Instance);

        await Task.Delay(300);

        vm.ShowUpgradeCta.Should().BeTrue();
    }

    [Fact]
    public async Task ShowUpgradeCta_False_WhenTrialStillActive()
    {
        var planSvc = new TestPlanService
        {
            Plan = ClientPlanType.FreeBYOK,
            ClientStatus = ClientSubscriptionStatus.Trial,
            TrialEndsAtUtc = DateTime.UtcNow.AddHours(2),
        };

        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.IsAuthenticated).Returns(true);

        var vm = new PlanSelectionViewModel(planSvc, auth.Object, NullLogger<PlanSelectionViewModel>.Instance);

        await Task.Delay(300);

        vm.ShowUpgradeCta.Should().BeFalse();
    }

    [Fact]
    public async Task ShowUpgradeCta_False_WhenOnCloudManaged()
    {
        var planSvc = new TestPlanService
        {
            Plan = ClientPlanType.CloudManaged,
            ClientStatus = ClientSubscriptionStatus.Active,
            TrialEndsAtUtc = null,
        };

        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.IsAuthenticated).Returns(true);

        var vm = new PlanSelectionViewModel(planSvc, auth.Object, NullLogger<PlanSelectionViewModel>.Instance);

        await Task.Delay(300);

        vm.ShowUpgradeCta.Should().BeFalse();
    }
}

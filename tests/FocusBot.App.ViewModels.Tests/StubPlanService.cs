using FocusBot.Core.Interfaces;

namespace FocusBot.App.ViewModels.Tests.FocusPageViewModelTests;

internal sealed class StubPlanService : IPlanService
{
#pragma warning disable CS0067
    public event EventHandler<ClientPlanType>? PlanChanged;
#pragma warning restore CS0067

    public Task<ClientPlanType> GetCurrentPlanAsync(CancellationToken ct = default) =>
        Task.FromResult(ClientPlanType.FreeBYOK);

    public Task RefreshAsync(CancellationToken ct = default) => Task.CompletedTask;

    public bool IsCloudPlan(ClientPlanType plan) =>
        plan is ClientPlanType.CloudBYOK or ClientPlanType.CloudManaged;
}

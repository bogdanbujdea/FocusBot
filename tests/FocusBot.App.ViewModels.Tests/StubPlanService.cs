using FocusBot.Core.Interfaces;

namespace FocusBot.App.ViewModels.Tests.FocusPageViewModelTests;

internal sealed class StubPlanService : IPlanService
{
    public event EventHandler<ClientPlanType>? PlanChanged;

    public ClientPlanType CurrentPlan { get; set; } = ClientPlanType.FreeBYOK;

    public ClientSubscriptionStatus Status { get; set; } = ClientSubscriptionStatus.Trial;
    public DateTime? TrialEndsAtUtc { get; set; } = DateTime.UtcNow.AddHours(24);
    public DateTime? CurrentPeriodEndsAtUtc { get; set; }

    public Task<ClientPlanType> GetCurrentPlanAsync(CancellationToken ct = default) =>
        Task.FromResult(CurrentPlan);

    public void RaisePlanChanged(ClientPlanType plan) =>
        PlanChanged?.Invoke(this, plan);

    public Task RefreshAsync(CancellationToken ct = default) => Task.CompletedTask;

    public bool IsCloudPlan(ClientPlanType plan) =>
        plan is ClientPlanType.CloudBYOK or ClientPlanType.CloudManaged;

    public Task<ClientSubscriptionStatus> GetStatusAsync(CancellationToken ct = default) =>
        Task.FromResult(Status);

    public Task<DateTime?> GetTrialEndsAtAsync(CancellationToken ct = default) =>
        Task.FromResult(TrialEndsAtUtc);

    public Task<DateTime?> GetCurrentPeriodEndsAtAsync(CancellationToken ct = default) =>
        Task.FromResult(CurrentPeriodEndsAtUtc);
}

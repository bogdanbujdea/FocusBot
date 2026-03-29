using FocusBot.Core.Interfaces;

namespace FocusBot.App.ViewModels.Tests.FocusPageViewModelTests;

internal sealed class FakeFocusHubClient : IFocusHubClient
{
    public event Action<SessionStartedEvent>? SessionStarted;
    public event Action<SessionEndedEvent>? SessionEnded;
    public event Action<SessionPausedEvent>? SessionPaused;
    public event Action<SessionResumedEvent>? SessionResumed;
    public event Action<PlanChangedEvent>? PlanChanged;

    public bool IsConnected { get; set; }

    public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task DisconnectAsync() => Task.CompletedTask;

    public void RaiseSessionStarted(SessionStartedEvent e) => SessionStarted?.Invoke(e);

    public void RaiseSessionEnded(SessionEndedEvent e) => SessionEnded?.Invoke(e);

    public void RaiseSessionPaused(SessionPausedEvent e) => SessionPaused?.Invoke(e);

    public void RaiseSessionResumed(SessionResumedEvent e) => SessionResumed?.Invoke(e);

    public void RaisePlanChanged(PlanChangedEvent e) => PlanChanged?.Invoke(e);
}

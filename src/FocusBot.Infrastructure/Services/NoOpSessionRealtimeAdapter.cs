using FocusBot.Core.Interfaces;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// No-op implementation of ISessionRealtimeAdapter for phase 1.
/// Phase 2 will replace this with SignalRSessionRealtimeAdapter.
/// </summary>
public class NoOpSessionRealtimeAdapter : ISessionRealtimeAdapter
{
    public Task ConnectAsync() => Task.CompletedTask;

    public Task DisconnectAsync() => Task.CompletedTask;
}

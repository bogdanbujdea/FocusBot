namespace FocusBot.Core.Interfaces;

/// <summary>
/// Adapter for real-time session synchronization (e.g., SignalR).
/// Phase 2: implementations will listen to remote events and reconcile with coordinator.
/// Phase 1: use NoOpSessionRealtimeAdapter.
/// </summary>
public interface ISessionRealtimeAdapter
{
    /// <summary>
    /// Connect to the real-time service.
    /// </summary>
    Task ConnectAsync();

    /// <summary>
    /// Disconnect from the real-time service.
    /// </summary>
    Task DisconnectAsync();
}

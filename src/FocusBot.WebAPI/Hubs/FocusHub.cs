using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FocusBot.WebAPI.Hubs;

/// <summary>
/// SignalR hub for real-time focus session notifications.
/// Clients join a per-user group so all devices for the same user
/// receive session lifecycle events.
/// </summary>
[Authorize]
public class FocusHub : Hub<IFocusHubClient>
{
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId is not null)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
        await base.OnDisconnectedAsync(exception);
    }

    private string? GetUserId() =>
        Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? Context.User?.FindFirstValue("sub");
}

/// <summary>
/// Typed client interface for methods the server can invoke on connected clients.
/// </summary>
public interface IFocusHubClient
{
    Task SessionStarted(SessionStartedEvent e);
    Task SessionEnded(SessionEndedEvent e);
    Task SessionPaused(SessionPausedEvent e);
    Task SessionResumed(SessionResumedEvent e);
    Task PlanChanged(PlanChangedEvent e);
    Task ClassificationChanged(ClassificationChangedEvent e);
}

public sealed record SessionStartedEvent(
    Guid SessionId,
    string SessionTitle,
    string? SessionContext,
    DateTime StartedAtUtc,
    string Source,
    Guid? OriginClientId = null
);

public sealed record SessionEndedEvent(
    Guid SessionId,
    DateTime EndedAtUtc,
    string Source
);

public sealed record SessionPausedEvent(
    Guid SessionId,
    DateTime PausedAtUtc,
    string Source
);

public sealed record SessionResumedEvent(
    Guid SessionId,
    string Source
);

/// <summary>Raised when the user's subscription or plan changed (e.g. Paddle webhook).</summary>
public sealed record PlanChangedEvent();

/// <summary>
/// Raised after a successful POST /classify (including coalesced batches) so all connected
/// clients can mirror the same alignment state across devices.
/// </summary>
/// <param name="Source">"extension" when the classified request carried a URL; otherwise "desktop".</param>
/// <param name="ActivityName">URL for extension requests, or process/window context for desktop.</param>
public sealed record ClassificationChangedEvent(
    int Score,
    string Reason,
    string Source,
    string ActivityName,
    DateTime ClassifiedAtUtc,
    bool Cached
);

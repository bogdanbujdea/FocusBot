using System.Collections.Concurrent;
using FocusBot.WebAPI.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace FocusBot.WebAPI.Features.Classification;

/// <summary>
/// Coalesces multiple classification requests per user into a single provider call
/// within a short time window, then fan-outs the same result to all callers.
/// </summary>
public sealed class ClassificationCoalescingService(
    IServiceScopeFactory scopeFactory,
    IHubContext<FocusHub, IFocusHubClient> hubContext,
    ILogger<ClassificationCoalescingService> logger)
{
    private static readonly TimeSpan CoalescingWindow = TimeSpan.FromSeconds(1);
    private readonly ConcurrentDictionary<Guid, UserQueueState> _queues = new();

    public Task<ClassifyResponse> EnqueueAndWaitAsync(
        Guid userId,
        ClassifyRequest request,
        string? byokApiKey,
        CancellationToken ct)
    {
        var queue = _queues.GetOrAdd(userId, _ => new UserQueueState());
        var pending = new PendingRequest(request, byokApiKey);

        lock (queue.Gate)
        {
            queue.Pending.Add(pending);
            if (!queue.IsWindowScheduled)
            {
                queue.IsWindowScheduled = true;
                _ = ProcessWindowAsync(userId, queue);
            }
        }

        if (ct.CanBeCanceled)
        {
            ct.Register(
                static state =>
                {
                    var requestState = (PendingRequest)state!;
                    requestState.Completion.TrySetCanceled();
                },
                pending
            );
        }

        return pending.Completion.Task;
    }

    private async Task ProcessWindowAsync(Guid userId, UserQueueState queue)
    {
        List<PendingRequest> active = [];

        try
        {
            await Task.Delay(CoalescingWindow).ConfigureAwait(false);

            List<PendingRequest> batch;
            lock (queue.Gate)
            {
                batch = [.. queue.Pending];
                queue.Pending.Clear();
                queue.IsWindowScheduled = false;
            }

            active = batch
                .Where(p => !p.Completion.Task.IsCompleted)
                .ToList();

            if (active.Count == 0)
            {
                CleanupQueueIfIdle(userId, queue);
                return;
            }

            var selected = PickBestRequest(active);
            var result = await ClassifyAsync(userId, selected.Request, selected.ByokApiKey).ConfigureAwait(false);

            await BroadcastClassificationAsync(userId, selected.Request, result).ConfigureAwait(false);

            foreach (var pending in active)
            {
                pending.Completion.TrySetResult(result);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Classification coalescing failed for user {UserId}", userId);

            foreach (var pending in active.Where(p => !p.Completion.Task.IsCompleted))
            {
                pending.Completion.TrySetException(ex);
            }
        }
        finally
        {
            CleanupQueueIfIdle(userId, queue);
        }
    }

    private async Task<ClassifyResponse> ClassifyAsync(
        Guid userId,
        ClassifyRequest request,
        string? byokApiKey)
    {
        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ClassificationService>();
        return await service.ClassifyAsync(userId, request, byokApiKey).ConfigureAwait(false);
    }

    private async Task BroadcastClassificationAsync(
        Guid userId,
        ClassifyRequest selectedRequest,
        ClassifyResponse result)
    {
        var (source, activityName) = ClassificationBroadcastHelper.Describe(selectedRequest);
        var evt = new ClassificationChangedEvent(
            result.Score,
            result.Reason,
            source,
            activityName,
            DateTime.UtcNow,
            result.Cached);

        try
        {
            await hubContext
                .Clients.Group(userId.ToString())
                .ClassificationChanged(evt)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to broadcast ClassificationChanged for user {UserId}",
                userId);
        }
    }

    private static PendingRequest PickBestRequest(IReadOnlyList<PendingRequest> pending)
    {
        var nonBrowserDesktop = pending
            .Where(p => IsNonBrowserDesktopRequest(p.Request))
            .MaxBy(p => p.ReceivedAtUtc);
        if (nonBrowserDesktop is not null)
        {
            return nonBrowserDesktop;
        }

        var extension = pending
            .Where(p => IsExtensionRequest(p.Request))
            .MaxBy(p => p.ReceivedAtUtc);
        if (extension is not null)
        {
            return extension;
        }

        return pending.MaxBy(p => p.ReceivedAtUtc)!;
    }

    private static bool IsExtensionRequest(ClassifyRequest request) =>
        !string.IsNullOrWhiteSpace(request.Url);

    private static bool IsNonBrowserDesktopRequest(ClassifyRequest request) =>
        !string.IsNullOrWhiteSpace(request.ProcessName)
        && !IsBrowserProcess(request.ProcessName);

    private static bool IsBrowserProcess(string processName) =>
        processName.Equals("chrome", StringComparison.OrdinalIgnoreCase)
        || processName.Equals("msedge", StringComparison.OrdinalIgnoreCase)
        || processName.Equals("firefox", StringComparison.OrdinalIgnoreCase)
        || processName.Equals("brave", StringComparison.OrdinalIgnoreCase)
        || processName.Equals("opera", StringComparison.OrdinalIgnoreCase);

    private void CleanupQueueIfIdle(Guid userId, UserQueueState queue)
    {
        lock (queue.Gate)
        {
            if (queue.Pending.Count == 0 && !queue.IsWindowScheduled)
            {
                _queues.TryRemove(userId, out _);
            }
        }
    }

    private sealed class UserQueueState
    {
        public object Gate { get; } = new();
        public List<PendingRequest> Pending { get; } = [];
        public bool IsWindowScheduled { get; set; }
    }

    private sealed class PendingRequest
    {
        public PendingRequest(ClassifyRequest request, string? byokApiKey)
        {
            Request = request;
            ByokApiKey = byokApiKey;
            Completion = new TaskCompletionSource<ClassifyResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            ReceivedAtUtc = DateTime.UtcNow;
        }

        public ClassifyRequest Request { get; }
        public string? ByokApiKey { get; }
        public TaskCompletionSource<ClassifyResponse> Completion { get; }
        public DateTime ReceivedAtUtc { get; }
    }
}

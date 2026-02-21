using FocusBot.Core.Entities;
using FocusBot.Core.Helpers;
using FocusBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Decorator that adds cache-first behavior to ILlmService. Checks cache before calling the inner service.
/// </summary>
public class AlignmentClassificationCacheDecorator : ILlmService
{
    private readonly ILlmService _inner;
    private readonly IServiceScopeFactory _scopeFactory;

    public AlignmentClassificationCacheDecorator(ILlmService inner, IServiceScopeFactory scopeFactory)
    {
        _inner = inner;
        _scopeFactory = scopeFactory;
    }

    public async Task<ClassifyAlignmentResponse> ClassifyAlignmentAsync(
        string taskDescription,
        string? taskContext,
        string processName,
        string windowTitle,
        CancellationToken ct = default)
    {
        var contextHash = HashHelper.ComputeWindowContextHash(processName, windowTitle);
        var taskContentHash = HashHelper.ComputeTaskContentHash(taskDescription, taskContext);

        using (var scope = _scopeFactory.CreateScope())
        {
            var cache = scope.ServiceProvider.GetRequiredService<IAlignmentCacheRepository>();
            var cached = await cache.GetAsync(contextHash, taskContentHash);
            if (cached != null)
                return new ClassifyAlignmentResponse(new AlignmentResult { Score = cached.Score, Reason = cached.Reason }, null);
        }

        var response = await _inner.ClassifyAlignmentAsync(taskDescription, taskContext, processName, windowTitle, ct);
        if (response.Result == null)
            return response;

        var result = response.Result;
        var normalizedTitle = HashHelper.NormalizeWindowTitle(windowTitle);
        var windowContext = new WindowContext
        {
            ContextHash = contextHash,
            ProcessName = processName,
            WindowTitle = normalizedTitle
        };
        var entry = new AlignmentCacheEntry
        {
            ContextHash = contextHash,
            TaskContentHash = taskContentHash,
            Score = result.Score,
            Reason = result.Reason,
            CreatedAt = DateTime.UtcNow
        };

        using (var scope = _scopeFactory.CreateScope())
        {
            var cache = scope.ServiceProvider.GetRequiredService<IAlignmentCacheRepository>();
            await cache.SaveAsync(windowContext, entry);
        }

        return response;
    }

    public Task<ClassifyAlignmentResponse> ValidateCredentialsAsync(
        string apiKey,
        string providerId,
        string modelId,
        CancellationToken ct = default) =>
        _inner.ValidateCredentialsAsync(apiKey, providerId, modelId, ct);
}

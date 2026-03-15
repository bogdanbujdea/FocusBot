using System.Text.Json;
using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace FocusBot.Infrastructure.Services;

public class TaskSummaryService(IServiceScopeFactory scopeFactory) : ITaskSummaryService
{
    private const int FocusedScoreThreshold = 6;
    private const int DistractedScoreThreshold = 4;
    private const int ContextSwitchMaxDurationSeconds = 120;
    private const int TopDistractingAppsCount = 10;

    public async Task ComputeAndPersistSummaryAsync(string taskId)
    {
        using var scope = scopeFactory.CreateScope();
        var taskRepository = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        var distractionEventRepository = scope.ServiceProvider.GetRequiredService<IDistractionEventRepository>();

        var segments = (await taskRepository.GetFocusSegmentsForTaskAsync(taskId).ConfigureAwait(false)).ToList();
        var events = await distractionEventRepository.GetEventsForTaskAsync(taskId).ConfigureAwait(false);

        var focusedSeconds = segments
            .Where(s => s.AlignmentScore >= FocusedScoreThreshold)
            .Sum(s => s.DurationSeconds);

        var distractedSeconds = segments
            .Where(s => s.AlignmentScore < DistractedScoreThreshold)
            .Sum(s => s.DurationSeconds);

        var distractionCount = events.Count;

        var contextSwitchCostSeconds = segments
            .Where(s => s.DurationSeconds < ContextSwitchMaxDurationSeconds)
            .Sum(s => s.DurationSeconds);

        var topDistractingAppsJson = BuildTopDistractingAppsJson(events);

        await taskRepository.UpdateTaskSummaryAsync(
            taskId,
            focusedSeconds,
            distractedSeconds,
            distractionCount,
            contextSwitchCostSeconds,
            topDistractingAppsJson
        ).ConfigureAwait(false);

        await taskRepository.DeleteFocusSegmentsForTaskAsync(taskId).ConfigureAwait(false);
        await distractionEventRepository.DeleteDistractionEventsForTaskAsync(taskId).ConfigureAwait(false);
    }

    private static string? BuildTopDistractingAppsJson(IReadOnlyList<DistractionEvent> events)
    {
        if (events.Count == 0)
            return null;

        var byApp = events
            .GroupBy(e => e.ProcessName)
            .Select(g => new TopDistractingAppEntry(
                g.Key,
                g.Sum(e => e.DistractedDurationSecondsAtEmit),
                g.Count()
            ))
            .OrderByDescending(x => x.Seconds)
            .ThenByDescending(x => x.Count)
            .ThenBy(x => x.App)
            .Take(TopDistractingAppsCount)
            .ToList();

        if (byApp.Count == 0)
            return null;

        return JsonSerializer.Serialize(byApp, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    private record TopDistractingAppEntry(string App, int Seconds, int Count);
}

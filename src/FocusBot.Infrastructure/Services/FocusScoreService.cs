using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace FocusBot.Infrastructure.Services;

public sealed class FocusScoreService : IFocusScoreService
{
    private readonly Dictionary<string, FocusSegment> _segments = [];
    private string? _currentSegmentKey;
    private DateTime _currentSegmentStartTime;
    private string? _currentTaskId;
    private readonly IServiceScopeFactory _scopeFactory;

    // Pending segment tracking (score not yet known)
    private string? _pendingContextHash;
    private string? _pendingWindowTitle;
    private string? _pendingProcessName;
    private DateTime _pendingStartTime;
    private bool _hasPendingSegment;
    private bool _hasReceivedRealScore;

    public FocusScoreService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public bool HasRealScore => _hasReceivedRealScore || _segments.Count > 0;

    public void StartOrResumeSegment(string taskId, string contextHash, int alignmentScore,
        string? windowTitle, string? processName)
    {
        PauseCurrentSegment();
        _currentTaskId = taskId;
        _hasPendingSegment = false;
        _hasReceivedRealScore = true;
        var key = BuildKey(taskId, contextHash, alignmentScore);
        if (!_segments.TryGetValue(key, out _))
        {
            _segments[key] = new FocusSegment
            {
                TaskId = taskId,
                ContextHash = contextHash,
                AlignmentScore = alignmentScore,
                DurationSeconds = 0,
                WindowTitle = windowTitle,
                ProcessName = processName,
            };
        }
        _currentSegmentKey = key;
        _currentSegmentStartTime = DateTime.UtcNow;
    }

    public void StartPendingSegment(string taskId, string contextHash,
        string? windowTitle, string? processName)
    {
        PauseCurrentSegment();
        _currentTaskId = taskId;
        _hasPendingSegment = true;
        _pendingContextHash = contextHash;
        _pendingWindowTitle = windowTitle;
        _pendingProcessName = processName;
        _pendingStartTime = DateTime.UtcNow;
        _currentSegmentKey = null;
    }

    public void UpdatePendingSegmentScore(int alignmentScore)
    {
        if (!_hasPendingSegment || _currentTaskId == null || _pendingContextHash == null) return;

        var elapsed = (int)(DateTime.UtcNow - _pendingStartTime).TotalSeconds;
        var taskId = _currentTaskId;
        var contextHash = _pendingContextHash;
        var windowTitle = _pendingWindowTitle;
        var processName = _pendingProcessName;

        _hasPendingSegment = false;
        _hasReceivedRealScore = true;

        var key = BuildKey(taskId, contextHash, alignmentScore);
        if (!_segments.TryGetValue(key, out var segment))
        {
            segment = new FocusSegment
            {
                TaskId = taskId,
                ContextHash = contextHash,
                AlignmentScore = alignmentScore,
                DurationSeconds = 0,
                WindowTitle = windowTitle,
                ProcessName = processName,
            };
            _segments[key] = segment;
        }
        segment.DurationSeconds += elapsed;
        _currentSegmentKey = key;
        _currentSegmentStartTime = DateTime.UtcNow;
    }

    public void PauseCurrentSegment()
    {
        if (_hasPendingSegment)
        {
            // Discard pending time when pausing without a score (e.g., FocusBot window)
            _hasPendingSegment = false;
            _pendingContextHash = null;
            return;
        }

        if (_currentSegmentKey == null) return;

        var elapsed = (int)(DateTime.UtcNow - _currentSegmentStartTime).TotalSeconds;
        if (elapsed > 0 && _segments.TryGetValue(_currentSegmentKey, out var current))
        {
            current.DurationSeconds += elapsed;
        }
        _currentSegmentKey = null;
    }

    public int CalculateFocusScorePercent(string taskId)
    {
        var taskSegments = _segments.Values.Where(s => s.TaskId == taskId).ToList();
        var currentDuration = GetCurrentSegmentDurationSeconds();
        var totalSeconds = taskSegments.Sum(s => s.DurationSeconds) + currentDuration;

        if (totalSeconds == 0) return 0;

        var weightedSum = taskSegments.Sum(s => s.AlignmentScore * s.DurationSeconds);
        if (_currentSegmentKey != null && _segments.TryGetValue(_currentSegmentKey, out var current))
        {
            weightedSum += current.AlignmentScore * currentDuration;
        }

        return (int)Math.Round((double)weightedSum / totalSeconds * 10);
    }

    public int GetCurrentSegmentDurationSeconds()
    {
        if (_currentSegmentKey == null) return 0;
        return (int)(DateTime.UtcNow - _currentSegmentStartTime).TotalSeconds;
    }

    public async Task PersistSegmentsAsync()
    {
        var currentDuration = GetCurrentSegmentDurationSeconds();
        var toPersist = _segments.Values
            .Select(s =>
            {
                var duration = s.DurationSeconds;
                if (_currentSegmentKey != null &&
                    BuildKey(s.TaskId, s.ContextHash, s.AlignmentScore) == _currentSegmentKey)
                {
                    duration += currentDuration;
                }
                return new FocusSegment
                {
                    TaskId = s.TaskId,
                    ContextHash = s.ContextHash,
                    AlignmentScore = s.AlignmentScore,
                    DurationSeconds = duration,
                    WindowTitle = s.WindowTitle,
                    ProcessName = s.ProcessName,
                };
            })
            .Where(s => s.DurationSeconds > 0)
            .ToList();
        if (toPersist.Count == 0) return;
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        await repo.UpsertFocusSegmentsAsync(toPersist);
    }

    public async Task LoadSegmentsForTaskAsync(string taskId)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        var existing = await repo.GetFocusSegmentsForTaskAsync(taskId);
        _segments.Clear();
        foreach (var s in existing)
        {
            var key = BuildKey(s.TaskId, s.ContextHash, s.AlignmentScore);
            _segments[key] = new FocusSegment
            {
                Id = s.Id,
                TaskId = s.TaskId,
                ContextHash = s.ContextHash,
                AlignmentScore = s.AlignmentScore,
                DurationSeconds = s.DurationSeconds,
                WindowTitle = s.WindowTitle,
                ProcessName = s.ProcessName,
            };
        }
        _currentTaskId = taskId;
        _currentSegmentKey = null;
        _hasPendingSegment = false;
        _hasReceivedRealScore = _segments.Count > 0;
    }

    public void ClearTaskSegments(string taskId)
    {
        var keysToRemove = _segments.Keys.Where(k => k.StartsWith(taskId + "|", StringComparison.Ordinal)).ToList();
        foreach (var k in keysToRemove)
        {
            _segments.Remove(k);
        }
        if (_currentTaskId == taskId)
        {
            _currentSegmentKey = null;
            _hasPendingSegment = false;
            _hasReceivedRealScore = false;
        }
    }

    private static string BuildKey(string taskId, string contextHash, int score) =>
        $"{taskId}|{contextHash}|{score}";
}

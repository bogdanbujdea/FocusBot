using System.Text.Json;
using FocusBot.Core.Entities;
using FocusBot.Core.Helpers;
using FocusBot.Core.Interfaces;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Lightweight in-memory focus session tracker.
/// Records per-process focused/distracted time each second, detects distraction transitions,
/// and computes a time-weighted focus score. Replaces the deleted FocusScoreService,
/// DistractionDetectorService, TaskSummaryService, and DailyAnalyticsService.
/// </summary>
public sealed class LocalSessionTracker : ILocalSessionTracker
{
    private const int AlignedThreshold = 6; // Score >= 6 counts as aligned

    private readonly object _lock = new();

    private bool _isIdle;
    private bool _isPreviouslyAligned;
    private string _previousProcessName = string.Empty;

    private long _focusedSeconds;
    private long _distractedSeconds;
    private int _distractionCount;
    private int _contextSwitchCount;

    // Per-process time buckets
    private readonly Dictionary<string, long> _alignedPerProcess = new();
    private readonly Dictionary<string, long> _distractedPerProcess = new();

    public void Start(string taskText)
    {
        lock (_lock)
        {
            Reset();
        }
    }

    public void RecordClassification(string processName, AlignmentResult result)
    {
        lock (_lock)
        {
            if (_isIdle)
                return;

            var isAligned = result.Score >= AlignedThreshold;

            AccountTime(processName, isAligned);
            DetectDistractionTransition(isAligned);
            DetectContextSwitch(processName);

            _isPreviouslyAligned = isAligned;
            _previousProcessName = processName;
        }
    }

    public void HandleIdle(bool isIdle)
    {
        lock (_lock)
        {
            _isIdle = isIdle;
        }
    }

    public int GetFocusScore()
    {
        lock (_lock)
        {
            return ComputeFocusScore(_focusedSeconds, _distractedSeconds);
        }
    }

    public SessionSummary GetSessionSummary()
    {
        lock (_lock)
        {
            return new SessionSummary
            {
                FocusScorePercent = ComputeFocusScore(_focusedSeconds, _distractedSeconds),
                FocusedSeconds = _focusedSeconds,
                DistractedSeconds = _distractedSeconds,
                DistractionCount = _distractionCount,
                ContextSwitchCount = _contextSwitchCount,
                TopDistractingApps = SerializeTopApps(_distractedPerProcess),
                TopAlignedApps = SerializeTopApps(_alignedPerProcess),
            };
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _isIdle = false;
            _isPreviouslyAligned = false;
            _previousProcessName = string.Empty;
            _focusedSeconds = 0;
            _distractedSeconds = 0;
            _distractionCount = 0;
            _contextSwitchCount = 0;
            _alignedPerProcess.Clear();
            _distractedPerProcess.Clear();
        }
    }

    private void AccountTime(string processName, bool isAligned)
    {
        if (isAligned)
        {
            _focusedSeconds++;
            _alignedPerProcess.TryGetValue(processName, out var aligned);
            _alignedPerProcess[processName] = aligned + 1;
        }
        else
        {
            _distractedSeconds++;
            _distractedPerProcess.TryGetValue(processName, out var distracted);
            _distractedPerProcess[processName] = distracted + 1;
        }
    }

    private void DetectDistractionTransition(bool isAligned)
    {
        if (_isPreviouslyAligned && !isAligned)
            _distractionCount++;
    }

    private void DetectContextSwitch(string processName)
    {
        if (!string.IsNullOrEmpty(_previousProcessName)
            && !string.Equals(_previousProcessName, processName, StringComparison.OrdinalIgnoreCase))
        {
            _contextSwitchCount++;
        }
    }

    private static int ComputeFocusScore(long focusedSeconds, long distractedSeconds) =>
        FocusScoreHelper.ComputeFocusScorePercentage(focusedSeconds, distractedSeconds);

    private static string? SerializeTopApps(Dictionary<string, long> perProcess, int topN = 5)
    {
        if (perProcess.Count == 0)
            return null;

        var top = perProcess
            .OrderByDescending(kv => kv.Value)
            .Take(topN)
            .Select(kv => new { app = kv.Key, seconds = kv.Value })
            .ToList();

        return JsonSerializer.Serialize(top);
    }
}

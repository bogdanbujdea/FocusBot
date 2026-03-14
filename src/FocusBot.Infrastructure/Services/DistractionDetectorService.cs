using FocusBot.Core.Entities;
using FocusBot.Core.Events;
using FocusBot.Core.Interfaces;

namespace FocusBot.Infrastructure.Services;

public sealed class DistractionDetectorService : IDistractionDetectorService
{
    private readonly IDistractionEventRepository _repository;

    private string? _currentTaskId;
    private string? _currentProcessName;
    private string? _currentWindowTitle;
    private DateTime? _distractedStartUtc;
    private bool _eventEmittedForCurrentEpisode;

    public DistractionDetectorService(IDistractionEventRepository repository)
    {
        _repository = repository;
    }

    public event EventHandler<DistractionEvent>? DistractionEventCreated;

    public async Task OnSampleAsync(
        string taskId,
        FocusStatus status,
        string processName,
        string windowTitle,
        DateTime sampleTimeUtc,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            ResetEpisode();
            return;
        }

        if (status == FocusStatus.Distracted)
        {
            await HandleDistractedSampleAsync(
                taskId,
                processName,
                windowTitle,
                sampleTimeUtc,
                cancellationToken);
        }
        else
        {
            ResetEpisode();
        }
    }

    private async Task HandleDistractedSampleAsync(
        string taskId,
        string processName,
        string windowTitle,
        DateTime sampleTimeUtc,
        CancellationToken cancellationToken)
    {
        var isSameEpisode =
            _currentTaskId == taskId &&
            string.Equals(_currentProcessName, processName, StringComparison.Ordinal) &&
            string.Equals(_currentWindowTitle, windowTitle, StringComparison.Ordinal);

        if (!isSameEpisode || _distractedStartUtc is null)
        {
            // New distracted candidate
            _currentTaskId = taskId;
            _currentProcessName = processName;
            _currentWindowTitle = windowTitle;
            _distractedStartUtc = sampleTimeUtc;
            _eventEmittedForCurrentEpisode = false;
            return;
        }

        if (_eventEmittedForCurrentEpisode)
        {
            // Already emitted for this continuous episode; do nothing while still distracted.
            return;
        }

        var seconds = (int)(sampleTimeUtc - _distractedStartUtc.Value).TotalSeconds;
        if (seconds < 5)
            return;

        var distractionEvent = new DistractionEvent
        {
            OccurredAtUtc = sampleTimeUtc,
            TaskId = taskId,
            ProcessName = processName,
            WindowTitleSnapshot = windowTitle,
            DistractedDurationSecondsAtEmit = seconds
        };

        await _repository.AddAsync(distractionEvent, cancellationToken).ConfigureAwait(false);
        _eventEmittedForCurrentEpisode = true;
        DistractionEventCreated?.Invoke(this, distractionEvent);
    }

    private void ResetEpisode()
    {
        _currentTaskId = null;
        _currentProcessName = null;
        _currentWindowTitle = null;
        _distractedStartUtc = null;
        _eventEmittedForCurrentEpisode = false;
    }
}


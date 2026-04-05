using FocusBot.Core.Entities;
using FocusBot.Core.Events;
using FocusBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Coordinates foreground window change detection with the classification API.
/// Called by <see cref="ISessionCoordinator"/> when session state changes.
/// </summary>
public sealed class ForegroundClassificationCoordinator : IForegroundClassificationCoordinator
{
    private readonly IWindowMonitorService _windowMonitor;
    private readonly IClassificationService _classificationService;
    private readonly ILogger<ForegroundClassificationCoordinator> _logger;

    private string _sessionTitle = string.Empty;
    private string? _sessionContext;
    private bool _isSubscribed;

    public event Action<ClassificationStatus>? ClassificationChanged;

    public ForegroundClassificationCoordinator(
        IWindowMonitorService windowMonitor,
        IClassificationService classificationService,
        ILogger<ForegroundClassificationCoordinator> logger)
    {
        _windowMonitor = windowMonitor;
        _classificationService = classificationService;
        _logger = logger;
    }

    public void Start(string sessionTitle, string? sessionContext)
    {
        if (_isSubscribed)
        {
            _logger.LogDebug("Already subscribed to foreground changes, updating session context");
            _sessionTitle = sessionTitle;
            _sessionContext = sessionContext;
            return;
        }

        _logger.LogInformation(
            "Starting foreground classification for session: {SessionTitle}",
            sessionTitle);

        _sessionTitle = sessionTitle;
        _sessionContext = sessionContext;
        _windowMonitor.ForegroundWindowChanged += OnForegroundWindowChanged;
        _isSubscribed = true;
    }

    public void Stop()
    {
        if (!_isSubscribed)
            return;

        _logger.LogInformation("Stopping foreground classification");
        _windowMonitor.ForegroundWindowChanged -= OnForegroundWindowChanged;
        _isSubscribed = false;
        _sessionTitle = string.Empty;
        _sessionContext = null;
    }

    private async void OnForegroundWindowChanged(object? sender, ForegroundWindowChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.ProcessName) && string.IsNullOrWhiteSpace(e.WindowTitle))
        {
            _logger.LogDebug("Empty foreground window info, skipping classification");
            return;
        }

        _logger.LogDebug(
            "Foreground changed: Process={ProcessName}, Window={WindowTitle}",
            e.ProcessName,
            e.WindowTitle);

        try
        {
            var result = await _classificationService.ClassifyAsync(
                e.ProcessName,
                e.WindowTitle,
                _sessionTitle,
                _sessionContext);

            if (result.IsFailure)
            {
                _logger.LogWarning("Classification failed: {Error}", result.Error);
                return;
            }

            var status = ClassificationStatus.FromScore(result.Value.Score, result.Value.Reason);
            ClassificationChanged?.Invoke(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during classification");
        }
    }
}

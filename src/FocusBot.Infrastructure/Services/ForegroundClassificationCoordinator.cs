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
    private static readonly HashSet<string> ChromiumBrowserProcesses = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "chrome",
        "msedge",
        "brave",
    };

    private static readonly HashSet<string> ExcludedProcesses = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "Foqus",
        "Taskmgr",
        "explorer",
    };

    private readonly IWindowMonitorService _windowMonitor;
    private readonly IClassificationService _classificationService;
    private readonly IExtensionPresenceService _presenceService;
    private readonly ILogger<ForegroundClassificationCoordinator> _logger;

    private string _sessionTitle = string.Empty;
    private string? _sessionContext;
    private bool _isSubscribed;

    public event Action<ForegroundContext>? ForegroundContextChanged;
    public event Action<ClassificationStatus>? ClassificationChanged;

    public ForegroundClassificationCoordinator(
        IWindowMonitorService windowMonitor,
        IClassificationService classificationService,
        IExtensionPresenceService presenceService,
        ILogger<ForegroundClassificationCoordinator> logger
    )
    {
        _windowMonitor = windowMonitor;
        _classificationService = classificationService;
        _presenceService = presenceService;
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
            sessionTitle
        );

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

    public void ApplyRemoteClassification(ClassificationChangedEvent evt)
    {
        _logger.LogDebug(
            "Applying remote classification from {Source}: Score={Score}, Activity={Activity}",
            evt.Source,
            evt.Score,
            evt.ActivityName
        );
        var status = ClassificationStatus.FromScore(evt.Score, evt.Reason, evt.Source);
        ClassificationChanged?.Invoke(status);
    }

    private async void OnForegroundWindowChanged(object? sender, ForegroundWindowChangedEventArgs e)
    {
        _logger.LogWarning($"Window changed to {e.WindowTitle}");
        if (string.IsNullOrWhiteSpace(e.ProcessName) && string.IsNullOrWhiteSpace(e.WindowTitle))
        {
            _logger.LogDebug("Empty foreground window info, skipping classification");
            return;
        }

        if (ExcludedProcesses.Contains(e.ProcessName))
        {
            _logger.LogDebug(
                "Skipping classification for excluded process: {Process}",
                e.ProcessName
            );
            return;
        }

        if (ChromiumBrowserProcesses.Contains(e.ProcessName) && _presenceService.IsExtensionOnline)
        {
            _logger.LogDebug(
                "Skipping classification for {Process} - extension is online",
                e.ProcessName
            );
            ForegroundContextChanged?.Invoke(
                new ForegroundContext(e.ProcessName, e.WindowTitle, IsClassifying: false)
            );
            return;
        }

        _logger.LogDebug(
            "Foreground changed: Process={ProcessName}, Window={WindowTitle}",
            e.ProcessName,
            e.WindowTitle
        );

        ForegroundContextChanged?.Invoke(
            new ForegroundContext(e.ProcessName, e.WindowTitle, IsClassifying: true)
        );

        try
        {
            var result = await _classificationService.ClassifyAsync(
                e.ProcessName,
                e.WindowTitle,
                _sessionTitle,
                _sessionContext
            );

            if (result.IsFailure)
            {
                _logger.LogWarning("Classification failed: {Error}", result.Error);
                ForegroundContextChanged?.Invoke(
                    new ForegroundContext(e.ProcessName, e.WindowTitle, IsClassifying: false)
                );
                return;
            }

            ForegroundContextChanged?.Invoke(
                new ForegroundContext(e.ProcessName, e.WindowTitle, IsClassifying: false)
            );

            var status = ClassificationStatus.FromScore(
                result.Value.Score,
                result.Value.Reason,
                "desktop"
            );
            ClassificationChanged?.Invoke(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during classification");
            ForegroundContextChanged?.Invoke(
                new ForegroundContext(e.ProcessName, e.WindowTitle, IsClassifying: false)
            );
        }
    }
}

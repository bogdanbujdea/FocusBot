using FocusBot.Core.Interfaces;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Service for managing the 24-hour free trial period.
/// Trial start time is stored in settings and persists across app restarts.
/// </summary>
public class TrialService : ITrialService
{
    private readonly ISettingsService _settingsService;
    private const string TrialStartedAtKey = "TrialStartedAt";
    private static readonly TimeSpan TrialDuration = TimeSpan.FromHours(24);

    public TrialService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<bool> HasTrialStartedAsync()
    {
        var startTime = await GetTrialStartTimeAsync();
        return startTime.HasValue;
    }

    public async Task<bool> IsTrialActiveAsync()
    {
        var startTime = await GetTrialStartTimeAsync();
        if (!startTime.HasValue)
            return false;

        var endTime = startTime.Value.Add(TrialDuration);
        return DateTime.UtcNow < endTime;
    }

    public async Task<bool> IsTrialExpiredAsync()
    {
        var startTime = await GetTrialStartTimeAsync();
        if (!startTime.HasValue)
            return false;

        var endTime = startTime.Value.Add(TrialDuration);
        return DateTime.UtcNow >= endTime;
    }

    public async Task<DateTime?> GetTrialEndTimeAsync()
    {
        var startTime = await GetTrialStartTimeAsync();
        if (!startTime.HasValue)
            return null;

        return startTime.Value.Add(TrialDuration);
    }

    public async Task<TimeSpan> GetTrialTimeRemainingAsync()
    {
        var endTime = await GetTrialEndTimeAsync();
        if (!endTime.HasValue)
            return TimeSpan.Zero;

        var remaining = endTime.Value - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    public async Task StartTrialAsync()
    {
        var alreadyStarted = await HasTrialStartedAsync();
        if (alreadyStarted)
            return;

        await _settingsService.SetSettingAsync(TrialStartedAtKey, DateTime.UtcNow);
    }

    private async Task<DateTime?> GetTrialStartTimeAsync()
    {
        var stored = await _settingsService.GetSettingAsync<DateTime?>(TrialStartedAtKey);
        return stored;
    }
}

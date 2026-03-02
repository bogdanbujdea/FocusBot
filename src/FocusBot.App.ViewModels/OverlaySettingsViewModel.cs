using CommunityToolkit.Mvvm.ComponentModel;
using FocusBot.Core.Interfaces;

namespace FocusBot.App.ViewModels;

/// <summary>
/// ViewModel for overlay settings section.
/// </summary>
public partial class OverlaySettingsViewModel : ObservableObject
{
    private const string OverlayEnabledKey = "OverlayEnabled";

    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private bool _isOverlayEnabled = true;

    /// <summary>
    /// Raised when overlay visibility setting changes.
    /// </summary>
    public event EventHandler<bool>? OverlayVisibilityChanged;

    public OverlaySettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _ = LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        var enabled = await _settingsService.GetSettingAsync<bool?>(OverlayEnabledKey);
        // Default to true if not set
        IsOverlayEnabled = enabled ?? true;
    }

    partial void OnIsOverlayEnabledChanged(bool value)
    {
        _ = _settingsService.SetSettingAsync(OverlayEnabledKey, value);
        OverlayVisibilityChanged?.Invoke(this, value);
    }
}

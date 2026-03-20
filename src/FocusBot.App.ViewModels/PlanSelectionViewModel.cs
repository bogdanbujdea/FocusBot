using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FocusBot.App.ViewModels;

/// <summary>
/// Manages plan display and tier selection in the settings view.
/// Shows the current plan, allows the user to open the billing portal or full analytics,
/// and polls plan state via <see cref="IPlanService"/>.
/// </summary>
public partial class PlanSelectionViewModel : ObservableObject
{
    private readonly IPlanService _planService;
    private readonly IAuthService _authService;
    private readonly ILogger<PlanSelectionViewModel> _logger;

    private const string WebAppAnalyticsUrl = "https://app.foqus.me/analytics";
    private const string WebAppBillingUrl = "https://app.foqus.me/billing";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFreePlan))]
    [NotifyPropertyChangedFor(nameof(IsCloudBYOKPlan))]
    [NotifyPropertyChangedFor(nameof(IsCloudManagedPlan))]
    [NotifyPropertyChangedFor(nameof(ShowFullAnalyticsCta))]
    [NotifyPropertyChangedFor(nameof(ShowUpgradeCta))]
    [NotifyPropertyChangedFor(nameof(PlanDisplayName))]
    [NotifyPropertyChangedFor(nameof(PlanDescription))]
    private ClientPlanType _currentPlan = ClientPlanType.FreeBYOK;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowFullAnalyticsCta))]
    [NotifyPropertyChangedFor(nameof(ShowUpgradeCta))]
    [NotifyPropertyChangedFor(nameof(ShowSignInPrompt))]
    [NotifyPropertyChangedFor(nameof(CanSelectPlan))]
    private bool _isSignedIn;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string _statusMessage = string.Empty;

    /// <summary>True when there is a non-empty status message to display.</summary>
    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

    /// <summary>True when the current plan is Free (BYOK, local-only).</summary>
    public bool IsFreePlan => CurrentPlan == ClientPlanType.FreeBYOK;

    /// <summary>True when the current plan is Cloud BYOK.</summary>
    public bool IsCloudBYOKPlan => CurrentPlan == ClientPlanType.CloudBYOK;

    /// <summary>True when the current plan is Cloud Managed.</summary>
    public bool IsCloudManagedPlan => CurrentPlan == ClientPlanType.CloudManaged;

    /// <summary>Show the "Open full analytics" button — visible for signed-in cloud users.</summary>
    public bool ShowFullAnalyticsCta => IsSignedIn && _planService.IsCloudPlan(CurrentPlan);

    /// <summary>Show the upgrade CTA — visible when not on a cloud plan.</summary>
    public bool ShowUpgradeCta => !_planService.IsCloudPlan(CurrentPlan);

    /// <summary>Show the sign-in prompt — user is on free plan and not signed in.</summary>
    public bool ShowSignInPrompt => !IsSignedIn && IsFreePlan;

    /// <summary>True if user can select plans (signed in).</summary>
    public bool CanSelectPlan => IsSignedIn;

    /// <summary>Short display name for the current plan.</summary>
    public string PlanDisplayName => CurrentPlan switch
    {
        ClientPlanType.FreeBYOK => "Free (BYOK)",
        ClientPlanType.CloudBYOK => "Cloud BYOK",
        ClientPlanType.CloudManaged => "Cloud Managed",
        _ => "Free",
    };

    /// <summary>One-line description of the current plan's capabilities.</summary>
    public string PlanDescription => CurrentPlan switch
    {
        ClientPlanType.FreeBYOK => "Your own API key · Local analytics only",
        ClientPlanType.CloudBYOK => "Your own API key · Full analytics · Cross-device sync",
        ClientPlanType.CloudManaged => "Platform API key · Full analytics · Cross-device sync",
        _ => string.Empty,
    };

    /// <summary>Opens the pricing/checkout page for the specified plan.</summary>
    [RelayCommand]
    private void SelectPlan(string planId)
    {
        var url = planId switch
        {
            "cloud-byok" => "https://app.foqus.me/checkout/cloud-byok",
            "cloud-managed" => "https://app.foqus.me/checkout/cloud-managed",
            _ => "https://foqus.me/pricing"
        };
        OpenUrl(url);
    }

    public PlanSelectionViewModel(
        IPlanService planService,
        IAuthService authService,
        ILogger<PlanSelectionViewModel> logger)
    {
        _planService = planService;
        _authService = authService;
        _logger = logger;

        _planService.PlanChanged += OnPlanChanged;
        _authService.AuthStateChanged += OnAuthStateChanged;

        _ = LoadAsync();
    }

    /// <summary>Forces a refresh of the plan from the backend.</summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task RefreshPlanAsync()
    {
        IsRefreshing = true;
        StatusMessage = string.Empty;
        try
        {
            await _planService.RefreshAsync();
            CurrentPlan = await _planService.GetCurrentPlanAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh plan status");
            StatusMessage = "Could not refresh plan status.";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    /// <summary>Opens the full analytics web app in the default browser.</summary>
    [RelayCommand]
    private void OpenFullAnalytics()
    {
        OpenUrl(WebAppAnalyticsUrl);
    }

    /// <summary>Opens the billing portal in the default browser.</summary>
    [RelayCommand]
    private void OpenBillingPortal()
    {
        OpenUrl(WebAppBillingUrl);
    }

    private async Task LoadAsync()
    {
        IsRefreshing = true;
        try
        {
            IsSignedIn = _authService.IsAuthenticated;
            CurrentPlan = await _planService.GetCurrentPlanAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load plan status on init");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private void OnPlanChanged(object? sender, ClientPlanType newPlan) => CurrentPlan = newPlan;

    private void OnAuthStateChanged() => IsSignedIn = _authService.IsAuthenticated;

    private static void OpenUrl(string url)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true,
        });
    }
}

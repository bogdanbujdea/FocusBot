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
    [NotifyPropertyChangedFor(nameof(PlanEndDateLabel))]
    private ClientPlanType _currentPlan = ClientPlanType.TrialFullAccess;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowFullAnalyticsCta))]
    [NotifyPropertyChangedFor(nameof(ShowUpgradeCta))]
    [NotifyPropertyChangedFor(nameof(ShowSignInPrompt))]
    [NotifyPropertyChangedFor(nameof(CanSelectPlan))]
    private bool _isSignedIn;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUpgradeCta))]
    [NotifyPropertyChangedFor(nameof(PlanDisplayName))]
    [NotifyPropertyChangedFor(nameof(PlanDescription))]
    [NotifyPropertyChangedFor(nameof(PlanEndDateLabel))]
    private ClientSubscriptionStatus _subscriptionStatus = ClientSubscriptionStatus.None;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUpgradeCta))]
    [NotifyPropertyChangedFor(nameof(PlanDisplayName))]
    [NotifyPropertyChangedFor(nameof(PlanDescription))]
    [NotifyPropertyChangedFor(nameof(PlanEndDateLabel))]
    private DateTime? _subscriptionTrialEndsAtUtc;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlanEndDateLabel))]
    private DateTime? _subscriptionCurrentPeriodEndsAtUtc;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string _statusMessage = string.Empty;

    /// <summary>True when there is a non-empty status message to display.</summary>
    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

    /// <summary>True when the current plan is Free (BYOK, local-only).</summary>
    public bool IsFreePlan => CurrentPlan == ClientPlanType.TrialFullAccess;

    /// <summary>True when the current plan is Foqus BYOK.</summary>
    public bool IsCloudBYOKPlan => CurrentPlan == ClientPlanType.CloudBYOK;

    /// <summary>True when the current plan is Foqus Premium.</summary>
    public bool IsCloudManagedPlan => CurrentPlan == ClientPlanType.CloudManaged;

    /// <summary>Show the "Open full analytics" button — visible for signed-in cloud users.</summary>
    public bool ShowFullAnalyticsCta => IsSignedIn && _planService.IsCloudPlan(CurrentPlan);

    /// <summary>Show the upgrade CTA after the Foqus trial ended and the user has no paid cloud plan.</summary>
    public bool ShowUpgradeCta =>
        IsSignedIn
        && !_planService.IsCloudPlan(CurrentPlan)
        && (
            SubscriptionStatus == ClientSubscriptionStatus.Expired
            || (
                SubscriptionStatus == ClientSubscriptionStatus.Trial
                && SubscriptionTrialEndsAtUtc.HasValue
                && ToUtc(SubscriptionTrialEndsAtUtc.Value) <= DateTime.UtcNow
            )
        );

    /// <summary>Show the sign-in prompt — user is on free plan and not signed in.</summary>
    public bool ShowSignInPrompt => !IsSignedIn && IsFreePlan;

    /// <summary>True if user can select plans (signed in).</summary>
    public bool CanSelectPlan => IsSignedIn;

    /// <summary>Short display name for the current plan.</summary>
    public string PlanDisplayName
    {
        get
        {
            if (
                CurrentPlan == ClientPlanType.TrialFullAccess
                && SubscriptionStatus == ClientSubscriptionStatus.Trial
                && SubscriptionTrialEndsAtUtc.HasValue
                && ToUtc(SubscriptionTrialEndsAtUtc.Value) > DateTime.UtcNow
            )
                return "Trial — Full Access";

            return CurrentPlan switch
            {
                ClientPlanType.TrialFullAccess => "Foqus trial",
                ClientPlanType.CloudBYOK => "Foqus BYOK",
                ClientPlanType.CloudManaged => "Foqus Premium",
                _ => "Foqus trial",
            };
        }
    }

    /// <summary>One-line description of the current plan's capabilities.</summary>
    public string PlanDescription
    {
        get
        {
            if (
                CurrentPlan == ClientPlanType.TrialFullAccess
                && SubscriptionStatus == ClientSubscriptionStatus.Trial
                && SubscriptionTrialEndsAtUtc.HasValue
                && ToUtc(SubscriptionTrialEndsAtUtc.Value) > DateTime.UtcNow
            )
                return "Full access · Choose a plan when the trial ends";

            return CurrentPlan switch
            {
                ClientPlanType.TrialFullAccess => "Sign in for cloud sync and analytics",
                ClientPlanType.CloudBYOK => "Your own API key · Full analytics · Cross-device sync",
                ClientPlanType.CloudManaged =>
                    "Platform API key · Full analytics · Cross-device sync",
                _ => string.Empty,
            };
        }
    }

    /// <summary>Human-readable end date for the currently displayed plan state.</summary>
    public string PlanEndDateLabel
    {
        get
        {
            DateTime? endUtc = null;

            if (SubscriptionStatus == ClientSubscriptionStatus.Trial)
                endUtc = SubscriptionTrialEndsAtUtc;
            else if (
                SubscriptionStatus
                is ClientSubscriptionStatus.Active
                    or ClientSubscriptionStatus.Canceled
            )
                endUtc = SubscriptionCurrentPeriodEndsAtUtc;

            if (!endUtc.HasValue)
                return "End date: unavailable";

            var local = ToUtc(endUtc.Value).ToLocalTime();
            return $"End date: {local:MMM d, h:mm tt}";
        }
    }

    private static DateTime ToUtc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();

    /// <summary>Opens the pricing/checkout page for the specified plan.</summary>
    [RelayCommand]
    private void SelectPlan(string planId)
    {
        var url = planId is "cloud-byok" or "cloud-managed"
            ? WebAppBillingUrl
            : "https://foqus.me/pricing";
        OpenUrl(url);
    }

    public PlanSelectionViewModel(
        IPlanService planService,
        IAuthService authService,
        ILogger<PlanSelectionViewModel> logger
    )
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
            await SyncSubscriptionDetailsAsync();
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
            await SyncSubscriptionDetailsAsync();
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

    private async Task SyncSubscriptionDetailsAsync()
    {
        SubscriptionStatus = await _planService.GetStatusAsync();
        SubscriptionTrialEndsAtUtc = await _planService.GetTrialEndsAtAsync();
        SubscriptionCurrentPeriodEndsAtUtc = await _planService.GetCurrentPeriodEndsAtAsync();
    }

    private void OnPlanChanged(object? sender, ClientPlanType newPlan)
    {
        CurrentPlan = newPlan;
        _ = SyncSubscriptionDetailsAsync();
    }

    private void OnAuthStateChanged() => IsSignedIn = _authService.IsAuthenticated;

    private static void OpenUrl(string url)
    {
        System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true }
        );
    }
}

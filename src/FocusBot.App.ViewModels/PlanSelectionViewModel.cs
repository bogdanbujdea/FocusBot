using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusBot.Core.Interfaces;
using FocusBot.WebAPI.Data.Entities;
using Microsoft.Extensions.Logging;

namespace FocusBot.App.ViewModels;

/// <summary>
/// Manages plan display and tier selection in the settings view.
/// Shows the current plan, allows the user to open the billing portal or full analytics
/// </summary>
public partial class PlanSelectionViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IFocusBotApiClient _focusBotApiClient;
    private readonly ILogger<PlanSelectionViewModel> _logger;

    private const string WebAppAnalyticsUrl = "https://app.foqus.me/analytics";
    private const string WebAppBillingUrl = "https://app.foqus.me/billing";

    public PlanSelectionViewModel(
        IAuthService authService,
        IFocusBotApiClient focusBotApiClient,
        ILogger<PlanSelectionViewModel> logger
    )
    {
        _authService = authService;
        _focusBotApiClient = focusBotApiClient;
        _logger = logger;

        _authService.AuthStateChanged += OnAuthStateChanged;

        _ = LoadAsync();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCloudBYOKPlan))]
    [NotifyPropertyChangedFor(nameof(IsCloudManagedPlan))]
    [NotifyPropertyChangedFor(nameof(ShowFullAnalyticsCta))]
    [NotifyPropertyChangedFor(nameof(ShowUpgradeCta))]
    [NotifyPropertyChangedFor(nameof(ShowPlanOptions))]
    [NotifyPropertyChangedFor(nameof(ShowUpgradeToCloudManaged))]
    [NotifyPropertyChangedFor(nameof(PlanDisplayName))]
    [NotifyPropertyChangedFor(nameof(PlanDescription))]
    [NotifyPropertyChangedFor(nameof(PlanEndDateLabel))]
    private PlanType _currentPlan = PlanType.TrialFullAccess;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowFullAnalyticsCta))]
    [NotifyPropertyChangedFor(nameof(ShowUpgradeCta))]
    [NotifyPropertyChangedFor(nameof(ShowPlanOptions))]
    [NotifyPropertyChangedFor(nameof(ShowUpgradeToCloudManaged))]
    [NotifyPropertyChangedFor(nameof(ShowSignInPrompt))]
    [NotifyPropertyChangedFor(nameof(CanSelectPlan))]
    private bool _isSignedIn;

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

    /// <summary>True when the current plan is Foqus BYOK.</summary>
    public bool IsCloudBYOKPlan => CurrentPlan == PlanType.CloudBYOK;

    /// <summary>True when the current plan is Foqus Premium.</summary>
    public bool IsCloudManagedPlan => CurrentPlan == PlanType.CloudManaged;

    /// <summary>Show the "Open full analytics" button — visible for signed-in cloud users.</summary>
    public bool ShowFullAnalyticsCta => IsSignedIn;

    /// <summary>Show the upgrade CTA after the Foqus trial ended and the user has no paid cloud plan.</summary>
    public bool ShowUpgradeCta => IsSignedIn && CurrentPlan == PlanType.TrialFullAccess;

    /// <summary>Show the sign-in prompt — user is on free plan and not signed in.</summary>
    public bool ShowSignInPrompt => !IsSignedIn;

    /// <summary>True if user can select plans (signed in).</summary>
    public bool CanSelectPlan => IsSignedIn;

    /// <summary>Show plan options (BYOK and Premium) when signed in and on trial.</summary>
    public bool ShowPlanOptions => IsSignedIn && CurrentPlan == PlanType.TrialFullAccess;

    /// <summary>Show upgrade to Cloud Managed button when signed in and on BYOK plan.</summary>
    public bool ShowUpgradeToCloudManaged => IsSignedIn && CurrentPlan == PlanType.CloudBYOK;

    /// <summary>Short display name for the current plan.</summary>
    public string PlanDisplayName
    {
        get
        {
            if (CurrentPlan == PlanType.TrialFullAccess)
                return "Trial — Full Access";

            return CurrentPlan switch
            {
                PlanType.TrialFullAccess => "Foqus trial",
                PlanType.CloudBYOK => "Foqus BYOK",
                PlanType.CloudManaged => "Foqus Premium",
                _ => "Foqus trial",
            };
        }
    }

    /// <summary>One-line description of the current plan's capabilities.</summary>
    public string PlanDescription
    {
        get
        {
            if (CurrentPlan == PlanType.TrialFullAccess)
                return "Full access · Choose a plan when the trial ends";

            return CurrentPlan switch
            {
                PlanType.TrialFullAccess => "Sign in for cloud sync and analytics",
                PlanType.CloudBYOK => "Your own API key · Full analytics · Cross-device sync",
                PlanType.CloudManaged => "Platform API key · Full analytics · Cross-device sync",
                _ => string.Empty,
            };
        }
    }

    /// <summary>
    /// Shows the expiration or end date for the current plan.
    /// Trial: "Expires at: Apr 5, 11:59 AM". Paid plans: "Renews: Apr 5, 11:59 AM".
    /// </summary>
    public string PlanEndDateLabel
    {
        get
        {
            if (!SubscriptionCurrentPeriodEndsAtUtc.HasValue)
                return string.Empty;

            var local = ToUtc(SubscriptionCurrentPeriodEndsAtUtc.Value).ToLocalTime();
            var dateStr = local.ToString("MMM d, h:mm tt");

            if (CurrentPlan == PlanType.TrialFullAccess)
                return $"Expires at: {dateStr}";

            return $"Renews: {dateStr}";
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

    /// <summary>Forces a refresh of the plan from the backend.</summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task RefreshPlanAsync()
    {
        IsRefreshing = true;
        StatusMessage = string.Empty;
        try
        {
            await LoadCurrentPlanAsync();
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

    private async Task LoadCurrentPlanAsync()
    {
        var user = await _focusBotApiClient.GetUserInfoAsync();
        if (user == null)
            return;
        CurrentPlan = user!.PlanType;
        if (CurrentPlan == PlanType.TrialFullAccess)
        {
            SubscriptionCurrentPeriodEndsAtUtc = user.CreatedAtUtc + TimeSpan.FromDays(1);
        }
        else
        {
            SubscriptionCurrentPeriodEndsAtUtc = user.SubscriptionEndDate;
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
            await LoadCurrentPlanAsync();
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

    private async void OnAuthStateChanged()
    {
        IsSignedIn = _authService.IsAuthenticated;
        if (IsSignedIn)
        {
            await LoadCurrentPlanAsync();
        }
        else
            ResetPlanUiAfterSignOut();
    }

    private void ResetPlanUiAfterSignOut()
    {
        CurrentPlan = PlanType.TrialFullAccess;
        SubscriptionCurrentPeriodEndsAtUtc = null;
    }

    private static void OpenUrl(string url)
    {
        System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true }
        );
    }
}

# Phase 2: Subscription Service

## Goal

Integrate with Windows Store to check subscription status and allow users to purchase a subscription. After this phase, users can subscribe via Windows Store, but the managed API key is not yet available (that's Phase 3).

## User Experience

After this phase, users will see:

**Not Subscribed:**
```
┌─────────────────────────────────────────────────────────────┐
│ ○ Subscribe to FocusBot Pro ($4.99/month)                   │
│                                                             │
│   ┌─────────────────────────────────────────────────────┐   │
│   │ Unlock AI-powered focus tracking without managing   │   │
│   │ API keys. $4.99/month, cancel anytime.              │   │
│   │                                                     │   │
│   │         [Subscribe Now]                             │   │
│   └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

**Subscribed:**
```
┌─────────────────────────────────────────────────────────────┐
│ ● Subscribe to FocusBot Pro ($4.99/month)                   │
│                                                             │
│   ✓ Active subscription                                     │
│   Renews on March 22, 2026                                  │
│                                                             │
│   [Manage Subscription]                                     │
│                                                             │
│   ⚠ Managed API key coming soon. Your subscription is      │
│     active and will work once this feature is released.     │
└─────────────────────────────────────────────────────────────┘
```

## What to Build

### 1. Core Layer: Subscription Interfaces

**File**: `src/FocusBot.Core/Interfaces/ISubscriptionService.cs`

```csharp
namespace FocusBot.Core.Interfaces;

/// <summary>
/// Service for managing subscription status via Windows Store.
/// </summary>
public interface ISubscriptionService
{
    /// <summary>
    /// Checks if the user has an active subscription.
    /// </summary>
    Task<bool> IsSubscribedAsync();
    
    /// <summary>
    /// Gets detailed subscription information.
    /// Returns null if not subscribed.
    /// </summary>
    Task<SubscriptionInfo?> GetSubscriptionInfoAsync();
    
    /// <summary>
    /// Opens the Windows Store purchase UI for the subscription.
    /// </summary>
    Task<PurchaseResult> PurchaseSubscriptionAsync();
    
    /// <summary>
    /// Opens the Microsoft account subscription management page.
    /// </summary>
    Task OpenManageSubscriptionAsync();
}
```

**File**: `src/FocusBot.Core/Entities/SubscriptionInfo.cs`

```csharp
namespace FocusBot.Core.Entities;

/// <summary>
/// Information about the user's subscription status.
/// </summary>
public record SubscriptionInfo
{
    /// <summary>
    /// Whether the subscription is currently active.
    /// </summary>
    public required bool IsActive { get; init; }
    
    /// <summary>
    /// When the current billing period ends.
    /// </summary>
    public required DateTimeOffset ExpirationDate { get; init; }
    
    /// <summary>
    /// Whether the subscription will auto-renew.
    /// </summary>
    public required bool WillAutoRenew { get; init; }
    
    /// <summary>
    /// True if the user is in a free trial period.
    /// </summary>
    public bool IsTrialPeriod { get; init; }
}
```

**File**: `src/FocusBot.Core/Entities/PurchaseResult.cs`

```csharp
namespace FocusBot.Core.Entities;

/// <summary>
/// Result of a subscription purchase attempt.
/// </summary>
public enum PurchaseResult
{
    /// <summary>
    /// Purchase completed successfully.
    /// </summary>
    Success,
    
    /// <summary>
    /// User cancelled the purchase.
    /// </summary>
    Cancelled,
    
    /// <summary>
    /// User already has this subscription.
    /// </summary>
    AlreadyOwned,
    
    /// <summary>
    /// Network error during purchase.
    /// </summary>
    NetworkError,
    
    /// <summary>
    /// Unknown error occurred.
    /// </summary>
    Error
}
```

### 2. Infrastructure Layer: Subscription Service

**File**: `src/FocusBot.Infrastructure/Services/SubscriptionService.cs`

```csharp
using Windows.Services.Store;
using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Manages subscription status using Windows Store APIs.
/// </summary>
public class SubscriptionService : ISubscriptionService
{
    private const string SubscriptionStoreId = "focusbot.subscription.monthly";
    
    private readonly ILogger<SubscriptionService> _logger;
    private StoreContext? _storeContext;
    
    public SubscriptionService(ILogger<SubscriptionService> logger)
    {
        _logger = logger;
    }
    
    private StoreContext GetStoreContext()
    {
        _storeContext ??= StoreContext.GetDefault();
        return _storeContext;
    }
    
    public async Task<bool> IsSubscribedAsync()
    {
        try
        {
            var info = await GetSubscriptionInfoAsync();
            return info?.IsActive ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check subscription status");
            return false;
        }
    }
    
    public async Task<SubscriptionInfo?> GetSubscriptionInfoAsync()
    {
        try
        {
            var context = GetStoreContext();
            var result = await context.GetStoreProductForCurrentAppAsync();
            
            if (result.ExtendedError != null)
            {
                _logger.LogWarning(result.ExtendedError, 
                    "Error getting store product");
                return null;
            }
            
            // Get add-ons for the current app
            var addOns = await context.GetAssociatedStoreProductsAsync(
                new[] { "Durable", "Consumable", "UnmanagedConsumable", "Subscription" });
            
            if (addOns.ExtendedError != null)
            {
                _logger.LogWarning(addOns.ExtendedError, 
                    "Error getting add-ons");
                return null;
            }
            
            // Find our subscription
            if (addOns.Products.TryGetValue(SubscriptionStoreId, out var subscription))
            {
                // Check if user owns this subscription
                var license = await context.GetAppLicenseAsync();
                
                if (license.AddOnLicenses.TryGetValue(SubscriptionStoreId, out var addOnLicense))
                {
                    return new SubscriptionInfo
                    {
                        IsActive = addOnLicense.IsActive,
                        ExpirationDate = addOnLicense.ExpirationDate,
                        WillAutoRenew = !addOnLicense.ExpirationDate.Equals(DateTimeOffset.MaxValue),
                        IsTrialPeriod = addOnLicense.IsTrialOwnedByThisUser
                    };
                }
            }
            
            return null; // Not subscribed
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get subscription info");
            return null;
        }
    }
    
    public async Task<PurchaseResult> PurchaseSubscriptionAsync()
    {
        try
        {
            var context = GetStoreContext();
            
            // Get the subscription product
            var result = await context.RequestPurchaseAsync(SubscriptionStoreId);
            
            return result.Status switch
            {
                StorePurchaseStatus.Succeeded => PurchaseResult.Success,
                StorePurchaseStatus.AlreadyPurchased => PurchaseResult.AlreadyOwned,
                StorePurchaseStatus.NotPurchased => PurchaseResult.Cancelled,
                StorePurchaseStatus.NetworkError => PurchaseResult.NetworkError,
                StorePurchaseStatus.ServerError => PurchaseResult.Error,
                _ => PurchaseResult.Error
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to purchase subscription");
            return PurchaseResult.Error;
        }
    }
    
    public Task OpenManageSubscriptionAsync()
    {
        // Open the Microsoft account subscriptions page
        var uri = new Uri("ms-windows-store://account/subscriptions");
        return Windows.System.Launcher.LaunchUriAsync(uri).AsTask();
    }
}
```

### 3. DI Registration

**File**: `src/FocusBot.App/App.xaml.cs` (or wherever services are registered)

Add to service registration:

```csharp
services.AddSingleton<ISubscriptionService, SubscriptionService>();
```

### 4. LlmService Update

**File**: `src/FocusBot.Infrastructure/Services/LlmService.cs`

Update to check subscription status:

```csharp
private readonly ISubscriptionService _subscriptionService;

public LlmService(
    ISettingsService settingsService, 
    ISubscriptionService subscriptionService,
    ILogger<LlmService> logger)
{
    _settingsService = settingsService;
    _subscriptionService = subscriptionService;
    _logger = logger;
}

public async Task<ClassifyAlignmentResponse> ClassifyAlignmentAsync(
    string taskDescription,
    string? taskContext,
    string processName,
    string windowTitle,
    CancellationToken ct = default)
{
    var mode = await _settingsService.GetApiKeyModeAsync();
    
    if (mode == ApiKeyMode.Managed)
    {
        var isSubscribed = await _subscriptionService.IsSubscribedAsync();
        
        if (!isSubscribed)
        {
            return new ClassifyAlignmentResponse(null, 
                "Please subscribe to use FocusBot Pro, or switch to using your own API key.");
        }
        
        // Phase 2: Subscribed but managed key not yet implemented
        return new ClassifyAlignmentResponse(null,
            "Your subscription is active! Managed API key support coming soon.");
    }
    
    // Own key mode - existing logic
    var apiKey = await _settingsService.GetApiKeyAsync();
    // ... rest of existing implementation
}
```

### 5. ViewModel Update

**File**: `src/FocusBot.App.ViewModels/ApiKeySettingsViewModel.cs`

Add subscription-related properties and commands:

```csharp
private readonly ISubscriptionService _subscriptionService;

[ObservableProperty]
private bool isSubscribed;

[ObservableProperty]
private SubscriptionInfo? subscriptionInfo;

[ObservableProperty]
private bool isLoadingSubscription;

[ObservableProperty]
private string? subscriptionStatusText;

[ObservableProperty]
private bool showSubscribeButton;

[ObservableProperty]
private bool showManageButton;

public ApiKeySettingsViewModel(
    ISettingsService settingsService,
    ISubscriptionService subscriptionService,
    ILlmService llmService)
{
    _settingsService = settingsService;
    _subscriptionService = subscriptionService;
    _llmService = llmService;
}

[RelayCommand]
private async Task LoadSubscriptionStatusAsync()
{
    IsLoadingSubscription = true;
    
    try
    {
        SubscriptionInfo = await _subscriptionService.GetSubscriptionInfoAsync();
        IsSubscribed = SubscriptionInfo?.IsActive ?? false;
        
        if (IsSubscribed && SubscriptionInfo != null)
        {
            var renewText = SubscriptionInfo.WillAutoRenew ? "Renews" : "Expires";
            SubscriptionStatusText = $"{renewText} on {SubscriptionInfo.ExpirationDate:MMMM d, yyyy}";
            ShowManageButton = true;
            ShowSubscribeButton = false;
        }
        else
        {
            SubscriptionStatusText = null;
            ShowManageButton = false;
            ShowSubscribeButton = true;
        }
    }
    finally
    {
        IsLoadingSubscription = false;
    }
}

[RelayCommand]
private async Task SubscribeAsync()
{
    IsLoadingSubscription = true;
    
    try
    {
        var result = await _subscriptionService.PurchaseSubscriptionAsync();
        
        switch (result)
        {
            case PurchaseResult.Success:
                await LoadSubscriptionStatusAsync();
                break;
            case PurchaseResult.AlreadyOwned:
                await LoadSubscriptionStatusAsync();
                break;
            case PurchaseResult.Cancelled:
                // User cancelled, no action needed
                break;
            case PurchaseResult.NetworkError:
                // Show network error message
                break;
            case PurchaseResult.Error:
                // Show generic error message
                break;
        }
    }
    finally
    {
        IsLoadingSubscription = false;
    }
}

[RelayCommand]
private async Task ManageSubscriptionAsync()
{
    await _subscriptionService.OpenManageSubscriptionAsync();
}

// Update initialization
public async Task InitializeAsync()
{
    await LoadSettingsAsync();
    await LoadSubscriptionStatusAsync();
}
```

### 6. Settings Page XAML Update

**File**: `src/FocusBot.App/Views/SettingsPage.xaml`

Update the subscription mode section:

```xml
<!-- Subscription Mode Content -->
<StackPanel Visibility="{x:Bind ViewModel.IsSubscriptionMode, Mode=OneWay,
                         Converter={StaticResource BoolToVisibility}}"
            Margin="28,0,0,0"
            Spacing="12">
    
    <!-- Loading indicator -->
    <ProgressRing IsActive="{x:Bind ViewModel.IsLoadingSubscription, Mode=OneWay}"
                  Width="24" Height="24"
                  Visibility="{x:Bind ViewModel.IsLoadingSubscription, Mode=OneWay,
                              Converter={StaticResource BoolToVisibility}}"/>
    
    <!-- Not subscribed state -->
    <StackPanel Visibility="{x:Bind ViewModel.ShowSubscribeButton, Mode=OneWay,
                             Converter={StaticResource BoolToVisibility}}"
                Spacing="12">
        <TextBlock TextWrapping="Wrap"
                   Foreground="{ThemeResource TextFillColorSecondaryBrush}">
            Unlock AI-powered focus tracking without managing API keys.
            $4.99/month, cancel anytime.
        </TextBlock>
        
        <Button Content="Subscribe Now"
                Style="{StaticResource AccentButtonStyle}"
                Command="{x:Bind ViewModel.SubscribeCommand}"/>
    </StackPanel>
    
    <!-- Subscribed state -->
    <StackPanel Visibility="{x:Bind ViewModel.IsSubscribed, Mode=OneWay,
                             Converter={StaticResource BoolToVisibility}}"
                Spacing="12">
        
        <StackPanel Orientation="Horizontal" Spacing="8">
            <FontIcon Glyph="&#xE73E;" 
                      Foreground="{ThemeResource SystemFillColorSuccessBrush}"/>
            <TextBlock Text="Active subscription" 
                       FontWeight="SemiBold"/>
        </StackPanel>
        
        <TextBlock Text="{x:Bind ViewModel.SubscriptionStatusText, Mode=OneWay}"
                   Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
        
        <Button Content="Manage Subscription"
                Command="{x:Bind ViewModel.ManageSubscriptionCommand}"/>
        
        <!-- Phase 2 notice: managed key not yet available -->
        <InfoBar IsOpen="True"
                 IsClosable="False"
                 Severity="Informational"
                 Title="Almost Ready"
                 Message="Your subscription is active! Managed API key support is coming soon. You can continue using your own API key in the meantime."/>
    </StackPanel>
</StackPanel>
```

## File Changes Summary

| File | Action | Description |
|------|--------|-------------|
| `src/FocusBot.Core/Interfaces/ISubscriptionService.cs` | Create | Subscription service interface |
| `src/FocusBot.Core/Entities/SubscriptionInfo.cs` | Create | Subscription info record |
| `src/FocusBot.Core/Entities/PurchaseResult.cs` | Create | Purchase result enum |
| `src/FocusBot.Infrastructure/Services/SubscriptionService.cs` | Create | Windows Store integration |
| `src/FocusBot.Infrastructure/Services/LlmService.cs` | Modify | Check subscription in managed mode |
| `src/FocusBot.App.ViewModels/ApiKeySettingsViewModel.cs` | Modify | Add subscription properties/commands |
| `src/FocusBot.App/Views/SettingsPage.xaml` | Modify | Add subscription UI |
| `src/FocusBot.App/App.xaml.cs` | Modify | Register subscription service |

## Windows Store Testing

### Sandbox Testing Setup

1. In Partner Center, create a test subscription add-on (or use flight ring)
2. Add test accounts in Partner Center → Your app → Test → Test accounts
3. On test device, sign in to Microsoft Store with test account
4. Test the purchase flow

### Test Without Partner Center

For development without a published add-on, create a mock implementation:

```csharp
public class MockSubscriptionService : ISubscriptionService
{
    private bool _isSubscribed = false;
    
    public Task<bool> IsSubscribedAsync() => Task.FromResult(_isSubscribed);
    
    public Task<SubscriptionInfo?> GetSubscriptionInfoAsync()
    {
        if (!_isSubscribed) return Task.FromResult<SubscriptionInfo?>(null);
        
        return Task.FromResult<SubscriptionInfo?>(new SubscriptionInfo
        {
            IsActive = true,
            ExpirationDate = DateTimeOffset.Now.AddDays(30),
            WillAutoRenew = true
        });
    }
    
    public Task<PurchaseResult> PurchaseSubscriptionAsync()
    {
        _isSubscribed = true;
        return Task.FromResult(PurchaseResult.Success);
    }
    
    public Task OpenManageSubscriptionAsync()
    {
        // No-op for mock
        return Task.CompletedTask;
    }
}
```

Register conditionally based on build configuration:

```csharp
#if DEBUG
services.AddSingleton<ISubscriptionService, MockSubscriptionService>();
#else
services.AddSingleton<ISubscriptionService, SubscriptionService>();
#endif
```

## Test Criteria

### Manual Testing

1. **Subscription Status Detection**
   - Not subscribed → Shows "Subscribe Now" button
   - Subscribed → Shows active status with renewal date
   - Expired subscription → Shows "Subscribe Now" button

2. **Purchase Flow**
   - Click "Subscribe Now" → Windows Store purchase UI appears
   - Complete purchase → UI updates to show active status
   - Cancel purchase → UI remains in not-subscribed state

3. **Manage Subscription**
   - Click "Manage Subscription" → Opens Microsoft account page

4. **Classification Behavior**
   - Managed mode + not subscribed → Shows "Please subscribe" message
   - Managed mode + subscribed → Shows "Coming soon" message (Phase 2 placeholder)

5. **Offline Handling**
   - App offline → Should gracefully handle, suggest Own Key mode

### Unit Tests

```csharp
[Fact]
public async Task IsSubscribedAsync_ReturnsFalse_WhenNoSubscription()
{
    // Test with mock that returns no subscription
}

[Fact]
public async Task ClassifyAlignmentAsync_PromptsSubscription_WhenManagedAndNotSubscribed()
{
    // Arrange
    var settingsMock = new Mock<ISettingsService>();
    settingsMock.Setup(s => s.GetApiKeyModeAsync())
                .ReturnsAsync(ApiKeyMode.Managed);
    
    var subMock = new Mock<ISubscriptionService>();
    subMock.Setup(s => s.IsSubscribedAsync())
           .ReturnsAsync(false);
    
    var service = new LlmService(settingsMock.Object, subMock.Object, /* ... */);
    
    // Act
    var result = await service.ClassifyAlignmentAsync("task", null, "proc", "title");
    
    // Assert
    Assert.Contains("subscribe", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
}
```

## Definition of Done

- [ ] `ISubscriptionService` interface created
- [ ] `SubscriptionInfo` and `PurchaseResult` entities created
- [ ] `SubscriptionService` implemented with Windows Store APIs
- [ ] `LlmService` checks subscription status for managed mode
- [ ] ViewModel has subscription-related properties and commands
- [ ] Settings UI shows subscription status and purchase button
- [ ] Mock service available for development testing
- [ ] Purchase flow works with Windows Store sandbox
- [ ] Manage subscription link works
- [ ] Unit tests pass
- [ ] Manual testing completed

## Known Limitations (Phase 2)

- Subscribed users cannot yet use the app without their own API key
- Managed key functionality comes in Phase 3
- UI shows "coming soon" notice for subscribed users

# Phase 1: Mode Selection Infrastructure

## Goal

Allow users to switch between "Own Key" (BYOK) and "Subscription" modes in the Settings page. This phase establishes the UI and data infrastructure without implementing actual subscription functionality.

## User Experience

After this phase, users will see:

```
┌─────────────────────────────────────────────────────────────┐
│ AI Provider Settings                                        │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│ How do you want to use AI features?                         │
│                                                             │
│ ● Use my own API key (Free)                                 │
│   ├─ Provider: [OpenAI           ▼]                         │
│   ├─ API Key:  [••••••••••••••••••]                         │
│   ├─ Model:    [gpt-4o-mini      ▼]                         │
│   └─ [Test Connection]                                      │
│                                                             │
│ ○ Subscribe to FocusBot Pro ($4.99/month)                   │
│   └─ Coming soon! Use your own API key for now.             │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

## What to Build

### 1. Core Layer: ApiKeyMode Enum

**File**: `src/FocusBot.Core/Entities/ApiKeyMode.cs`

```csharp
namespace FocusBot.Core.Entities;

/// <summary>
/// Specifies how the app obtains the API key for AI services.
/// </summary>
public enum ApiKeyMode
{
    /// <summary>
    /// User provides their own API key from their AI provider account.
    /// </summary>
    Own,
    
    /// <summary>
    /// User subscribes and uses a managed API key provided by the app.
    /// </summary>
    Managed
}
```

### 2. Settings Service Interface Update

**File**: `src/FocusBot.Core/Interfaces/ISettingsService.cs`

Add these methods to the existing interface:

```csharp
/// <summary>
/// Gets the current API key mode (Own or Managed).
/// </summary>
Task<ApiKeyMode> GetApiKeyModeAsync();

/// <summary>
/// Sets the API key mode.
/// </summary>
Task SetApiKeyModeAsync(ApiKeyMode mode);
```

### 3. Settings Service Implementation

**File**: `src/FocusBot.Infrastructure/Services/SettingsService.cs`

Implement the new methods:

```csharp
private const string ApiKeyModeKey = "ApiKeyMode";

public async Task<ApiKeyMode> GetApiKeyModeAsync()
{
    var stored = await GetSettingAsync(ApiKeyModeKey);
    if (string.IsNullOrEmpty(stored))
        return ApiKeyMode.Own; // Default to Own for existing users
    
    return Enum.TryParse<ApiKeyMode>(stored, out var mode) 
        ? mode 
        : ApiKeyMode.Own;
}

public async Task SetApiKeyModeAsync(ApiKeyMode mode)
{
    await SetSettingAsync(ApiKeyModeKey, mode.ToString());
}
```

### 4. LlmService Update

**File**: `src/FocusBot.Infrastructure/Services/LlmService.cs`

Modify `ClassifyAlignmentAsync` to check the mode:

```csharp
public async Task<ClassifyAlignmentResponse> ClassifyAlignmentAsync(
    string taskDescription,
    string? taskContext,
    string processName,
    string windowTitle,
    CancellationToken ct = default)
{
    var mode = await settingsService.GetApiKeyModeAsync();
    
    if (mode == ApiKeyMode.Managed)
    {
        // Phase 1: Subscription not yet implemented
        return new ClassifyAlignmentResponse(null, 
            "Subscription coming soon. Please use your own API key for now.");
    }
    
    // Existing Own key logic follows...
    var apiKey = await settingsService.GetApiKeyAsync();
    if (string.IsNullOrWhiteSpace(apiKey))
        return new ClassifyAlignmentResponse(null, null);
    
    // ... rest of existing implementation
}
```

### 5. Settings ViewModel Update

**File**: `src/FocusBot.App.ViewModels/ApiKeySettingsViewModel.cs`

Add properties and commands for mode selection:

```csharp
[ObservableProperty]
private ApiKeyMode apiKeyMode = ApiKeyMode.Own;

[ObservableProperty]
private bool isOwnKeyMode = true;

[ObservableProperty]
private bool isSubscriptionMode = false;

partial void OnApiKeyModeChanged(ApiKeyMode value)
{
    IsOwnKeyMode = value == ApiKeyMode.Own;
    IsSubscriptionMode = value == ApiKeyMode.Managed;
}

[RelayCommand]
private async Task SelectOwnKeyMode()
{
    ApiKeyMode = ApiKeyMode.Own;
    await _settingsService.SetApiKeyModeAsync(ApiKeyMode.Own);
}

[RelayCommand]
private async Task SelectSubscriptionMode()
{
    ApiKeyMode = ApiKeyMode.Managed;
    await _settingsService.SetApiKeyModeAsync(ApiKeyMode.Managed);
}

// Call this in initialization
private async Task LoadSettingsAsync()
{
    // ... existing loading code ...
    ApiKeyMode = await _settingsService.GetApiKeyModeAsync();
}
```

### 6. Settings Page XAML Update

**File**: `src/FocusBot.App/Views/SettingsPage.xaml`

Add radio button group for mode selection:

```xml
<StackPanel Spacing="16">
    <TextBlock Text="How do you want to use AI features?" 
               Style="{StaticResource SubtitleTextBlockStyle}"/>
    
    <!-- Own Key Mode -->
    <RadioButton x:Name="OwnKeyRadio"
                 GroupName="ApiKeyMode"
                 IsChecked="{x:Bind ViewModel.IsOwnKeyMode, Mode=TwoWay}"
                 Command="{x:Bind ViewModel.SelectOwnKeyModeCommand}">
        <StackPanel>
            <TextBlock Text="Use my own API key (Free)" 
                       FontWeight="SemiBold"/>
            <TextBlock Text="You provide an API key from OpenAI, Anthropic, etc."
                       Style="{StaticResource CaptionTextBlockStyle}"
                       Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
        </StackPanel>
    </RadioButton>
    
    <!-- API Key Fields (visible when Own mode selected) -->
    <StackPanel Visibility="{x:Bind ViewModel.IsOwnKeyMode, Mode=OneWay, 
                             Converter={StaticResource BoolToVisibility}}"
                Margin="28,0,0,0"
                Spacing="12">
        <!-- Existing provider, API key, model fields -->
        <!-- ... -->
    </StackPanel>
    
    <!-- Subscription Mode -->
    <RadioButton x:Name="SubscriptionRadio"
                 GroupName="ApiKeyMode"
                 IsChecked="{x:Bind ViewModel.IsSubscriptionMode, Mode=TwoWay}"
                 Command="{x:Bind ViewModel.SelectSubscriptionModeCommand}">
        <StackPanel>
            <TextBlock Text="Subscribe to FocusBot Pro ($4.99/month)" 
                       FontWeight="SemiBold"/>
            <TextBlock Text="No API key needed - we handle everything"
                       Style="{StaticResource CaptionTextBlockStyle}"
                       Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
        </StackPanel>
    </RadioButton>
    
    <!-- Subscription Placeholder (visible when Subscription mode selected) -->
    <StackPanel Visibility="{x:Bind ViewModel.IsSubscriptionMode, Mode=OneWay,
                             Converter={StaticResource BoolToVisibility}}"
                Margin="28,0,0,0">
        <InfoBar IsOpen="True"
                 IsClosable="False"
                 Severity="Informational"
                 Title="Coming Soon"
                 Message="Subscription support is coming soon. Please use your own API key for now."/>
    </StackPanel>
</StackPanel>
```

## File Changes Summary

| File | Action | Description |
|------|--------|-------------|
| `src/FocusBot.Core/Entities/ApiKeyMode.cs` | Create | New enum for API key modes |
| `src/FocusBot.Core/Interfaces/ISettingsService.cs` | Modify | Add mode getter/setter methods |
| `src/FocusBot.Infrastructure/Services/SettingsService.cs` | Modify | Implement mode storage |
| `src/FocusBot.Infrastructure/Services/LlmService.cs` | Modify | Check mode before classification |
| `src/FocusBot.App.ViewModels/ApiKeySettingsViewModel.cs` | Modify | Add mode properties and commands |
| `src/FocusBot.App/Views/SettingsPage.xaml` | Modify | Add mode selection UI |

## Test Criteria

### Manual Testing

1. **Mode Persistence**
   - Select "Own Key" mode, close and reopen app → Should remember Own Key
   - Select "Subscription" mode, close and reopen app → Should remember Subscription

2. **UI Visibility**
   - Select "Own Key" → API key fields should be visible
   - Select "Subscription" → "Coming soon" message should be visible, API key fields hidden

3. **Classification Behavior**
   - In "Own Key" mode with valid API key → Classification works normally
   - In "Own Key" mode without API key → Existing behavior (score defaults to 5)
   - In "Subscription" mode → Should show "Subscription coming soon" message

4. **Existing User Migration**
   - Users who upgrade from previous version → Should default to "Own Key" mode
   - Their existing API key should still work

### Unit Tests

```csharp
[Fact]
public async Task GetApiKeyModeAsync_DefaultsToOwn_WhenNotSet()
{
    var settings = new SettingsService(/* ... */);
    var mode = await settings.GetApiKeyModeAsync();
    Assert.Equal(ApiKeyMode.Own, mode);
}

[Fact]
public async Task SetApiKeyModeAsync_PersistsMode()
{
    var settings = new SettingsService(/* ... */);
    await settings.SetApiKeyModeAsync(ApiKeyMode.Managed);
    var mode = await settings.GetApiKeyModeAsync();
    Assert.Equal(ApiKeyMode.Managed, mode);
}

[Fact]
public async Task ClassifyAlignmentAsync_ReturnsPendingMessage_WhenManagedMode()
{
    // Arrange
    var settingsMock = new Mock<ISettingsService>();
    settingsMock.Setup(s => s.GetApiKeyModeAsync())
                .ReturnsAsync(ApiKeyMode.Managed);
    
    var service = new LlmService(settingsMock.Object, /* ... */);
    
    // Act
    var result = await service.ClassifyAlignmentAsync(
        "task", null, "process", "title");
    
    // Assert
    Assert.Null(result.Result);
    Assert.Contains("coming soon", result.ErrorMessage, 
                    StringComparison.OrdinalIgnoreCase);
}
```

## Definition of Done

- [ ] `ApiKeyMode` enum created in Core layer
- [ ] `ISettingsService` has new mode methods
- [ ] `SettingsService` implements mode persistence
- [ ] `LlmService` checks mode and returns placeholder for Managed
- [ ] Settings UI has radio button toggle
- [ ] API key fields show/hide based on selected mode
- [ ] Mode persists across app restarts
- [ ] Existing users default to Own Key mode
- [ ] Unit tests pass
- [ ] Manual testing completed

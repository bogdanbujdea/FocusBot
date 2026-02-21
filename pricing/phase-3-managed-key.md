# Phase 3: Managed Key Provider

## Goal

Provide subscribed users with a managed API key so they can use the app without configuring their own API key. This completes the end-to-end subscription flow.

## User Experience

After this phase, subscribed users selecting "FocusBot Pro" mode will:

1. Have AI classification work automatically
2. See no API key configuration fields
3. Experience the app exactly like BYOK users, but without setup friction

```
┌─────────────────────────────────────────────────────────────┐
│ ● Subscribe to FocusBot Pro ($4.99/month)                   │
│                                                             │
│   ✓ Active subscription                                     │
│   Renews on March 22, 2026                                  │
│                                                             │
│   [Manage Subscription]                                     │
│                                                             │
│   Your AI-powered focus tracking is active. No setup        │
│   required - we handle everything!                          │
└─────────────────────────────────────────────────────────────┘
```

## What to Build

### 1. Core Layer: Managed Key Provider Interface

**File**: `src/FocusBot.Core/Interfaces/IManagedKeyProvider.cs`

```csharp
namespace FocusBot.Core.Interfaces;

/// <summary>
/// Provides the managed API key for subscribed users.
/// </summary>
/// <remarks>
/// This interface abstracts the key source, allowing easy migration from
/// embedded key (MVP) to server-fetched key (production).
/// </remarks>
public interface IManagedKeyProvider
{
    /// <summary>
    /// Gets the managed API key.
    /// Returns null if the key is unavailable.
    /// </summary>
    Task<string?> GetApiKeyAsync();
    
    /// <summary>
    /// Gets the provider ID for the managed key (e.g., "openai").
    /// </summary>
    string ProviderId { get; }
    
    /// <summary>
    /// Gets the model ID for the managed key (e.g., "gpt-4o-mini").
    /// </summary>
    string ModelId { get; }
}
```

### 2. Infrastructure Layer: Embedded Key Provider (MVP)

**File**: `src/FocusBot.Infrastructure/Services/EmbeddedManagedKeyProvider.cs`

```csharp
using FocusBot.Core.Interfaces;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Provides a managed API key embedded in the application.
/// </summary>
/// <remarks>
/// WARNING: This is the MVP implementation. The key can be extracted by
/// determined attackers. For production at scale, migrate to
/// ServerProxyManagedKeyProvider.
/// 
/// Obfuscation techniques used:
/// - Key split into multiple parts
/// - Parts XOR'd with known values
/// - Assembly-level obfuscation recommended
/// </remarks>
public class EmbeddedManagedKeyProvider : IManagedKeyProvider
{
    // Use OpenAI gpt-4o-mini for managed users (cheapest option)
    public string ProviderId => "openai";
    public string ModelId => "gpt-4o-mini";
    
    public Task<string?> GetApiKeyAsync()
    {
        var key = ReconstructKey();
        return Task.FromResult<string?>(key);
    }
    
    private static string ReconstructKey()
    {
        // IMPORTANT: Replace these with your actual obfuscated key parts
        // This is an example of split + XOR obfuscation
        
        // The key "sk-proj-abc123..." is split and XOR'd
        // In practice, use a code obfuscator like ConfuserEx
        
        var part1 = Deobfuscate(
            new byte[] { /* XOR'd bytes for "sk-proj-" */ },
            GetXorKey1());
        
        var part2 = Deobfuscate(
            new byte[] { /* XOR'd bytes for middle part */ },
            GetXorKey2());
        
        var part3 = Deobfuscate(
            new byte[] { /* XOR'd bytes for end part */ },
            GetXorKey3());
        
        return string.Concat(part1, part2, part3);
    }
    
    private static string Deobfuscate(byte[] data, byte[] key)
    {
        var result = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            result[i] = (byte)(data[i] ^ key[i % key.Length]);
        }
        return System.Text.Encoding.UTF8.GetString(result);
    }
    
    // XOR keys - split across methods to make static analysis harder
    private static byte[] GetXorKey1() => new byte[] { 0x5A, 0x3C, 0x7E, 0x1B };
    private static byte[] GetXorKey2() => new byte[] { 0x2D, 0x8F, 0x4A, 0x6C };
    private static byte[] GetXorKey3() => new byte[] { 0x9E, 0x1A, 0x5B, 0x3D };
}
```

### 3. Key Obfuscation Helper

**File**: `src/FocusBot.Infrastructure/Services/KeyObfuscationHelper.cs`

Create a helper for generating obfuscated key data (use during development, not at runtime):

```csharp
namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Helper for generating obfuscated key data.
/// Use this during development to generate the byte arrays for EmbeddedManagedKeyProvider.
/// DO NOT include actual key generation logic in production builds.
/// </summary>
public static class KeyObfuscationHelper
{
#if DEBUG
    /// <summary>
    /// Generates obfuscated byte arrays for a given API key.
    /// Run this once, copy the output to EmbeddedManagedKeyProvider, then remove.
    /// </summary>
    public static void GenerateObfuscatedKey(string apiKey)
    {
        // Split key into 3 parts
        var partLength = apiKey.Length / 3;
        var part1 = apiKey.Substring(0, partLength);
        var part2 = apiKey.Substring(partLength, partLength);
        var part3 = apiKey.Substring(partLength * 2);
        
        var xorKey1 = new byte[] { 0x5A, 0x3C, 0x7E, 0x1B };
        var xorKey2 = new byte[] { 0x2D, 0x8F, 0x4A, 0x6C };
        var xorKey3 = new byte[] { 0x9E, 0x1A, 0x5B, 0x3D };
        
        Console.WriteLine("Part 1 bytes:");
        Console.WriteLine(FormatBytes(Obfuscate(part1, xorKey1)));
        
        Console.WriteLine("Part 2 bytes:");
        Console.WriteLine(FormatBytes(Obfuscate(part2, xorKey2)));
        
        Console.WriteLine("Part 3 bytes:");
        Console.WriteLine(FormatBytes(Obfuscate(part3, xorKey3)));
    }
    
    private static byte[] Obfuscate(string text, byte[] key)
    {
        var data = System.Text.Encoding.UTF8.GetBytes(text);
        var result = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            result[i] = (byte)(data[i] ^ key[i % key.Length]);
        }
        return result;
    }
    
    private static string FormatBytes(byte[] bytes)
    {
        return "new byte[] { " + string.Join(", ", bytes.Select(b => $"0x{b:X2}")) + " }";
    }
#endif
}
```

### 4. LlmService Update

**File**: `src/FocusBot.Infrastructure/Services/LlmService.cs`

Complete the managed key flow:

```csharp
private readonly ISettingsService _settingsService;
private readonly ISubscriptionService _subscriptionService;
private readonly IManagedKeyProvider _managedKeyProvider;
private readonly ILogger<LlmService> _logger;

public LlmService(
    ISettingsService settingsService,
    ISubscriptionService subscriptionService,
    IManagedKeyProvider managedKeyProvider,
    ILogger<LlmService> logger)
{
    _settingsService = settingsService;
    _subscriptionService = subscriptionService;
    _managedKeyProvider = managedKeyProvider;
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
    
    string apiKey;
    string providerId;
    string modelId;
    
    if (mode == ApiKeyMode.Managed)
    {
        // Check subscription status
        var isSubscribed = await _subscriptionService.IsSubscribedAsync();
        
        if (!isSubscribed)
        {
            return new ClassifyAlignmentResponse(null,
                "Please subscribe to use FocusBot Pro, or switch to using your own API key.");
        }
        
        // Get managed key
        var managedKey = await _managedKeyProvider.GetApiKeyAsync();
        if (string.IsNullOrWhiteSpace(managedKey))
        {
            _logger.LogError("Managed key provider returned null or empty key");
            return new ClassifyAlignmentResponse(null,
                "Unable to access AI service. Please try again or use your own API key.");
        }
        
        apiKey = managedKey;
        providerId = _managedKeyProvider.ProviderId;
        modelId = _managedKeyProvider.ModelId;
    }
    else // Own key mode
    {
        apiKey = await _settingsService.GetApiKeyAsync() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new ClassifyAlignmentResponse(null, null);
        }
        
        providerId = await _settingsService.GetProviderAsync()
            ?? LlmProviderConfig.DefaultProvider.ProviderId;
        modelId = await _settingsService.GetModelAsync()
            ?? LlmProviderConfig.DefaultModel(providerId).ModelId;
    }
    
    try
    {
        // Build the user message
        var userMessage = string.IsNullOrWhiteSpace(taskContext)
            ? $"Task: {taskDescription}\n\nCurrent window: Application = {processName}, Title = {windowTitle}"
            : $"Task: {taskDescription}\n\nContext provided by the user: {taskContext}\n\nCurrent window: Application = {processName}, Title = {windowTitle}";
        
        var provider = LlmProviderConfig.ToLlmProvider(providerId);
        var api = new TornadoApi(provider, apiKey);
        
        var response = await api
            .Chat.CreateConversation(modelId)
            .AppendSystemMessage(SystemPrompt)
            .AppendUserInput(userMessage)
            .GetResponse(ct);
        
        if (string.IsNullOrWhiteSpace(response))
            return new ClassifyAlignmentResponse(null, null);
        
        var result = ParseResponse(response);
        return result != null
            ? new ClassifyAlignmentResponse(result, null)
            : new ClassifyAlignmentResponse(null, "Invalid response from AI.");
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Alignment classification failed");
        var userMessage = ToUserFriendlyErrorMessage(ex.Message);
        return new ClassifyAlignmentResponse(null, userMessage);
    }
}
```

### 5. DI Registration

**File**: `src/FocusBot.App/App.xaml.cs`

Register the managed key provider:

```csharp
services.AddSingleton<IManagedKeyProvider, EmbeddedManagedKeyProvider>();
```

### 6. Update Settings UI

**File**: `src/FocusBot.App/Views/SettingsPage.xaml`

Remove the "coming soon" notice for subscribed users:

```xml
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
    
    <!-- Success message instead of "coming soon" -->
    <TextBlock TextWrapping="Wrap"
               Foreground="{ThemeResource TextFillColorSecondaryBrush}">
        Your AI-powered focus tracking is active. No setup required - we handle everything!
    </TextBlock>
</StackPanel>
```

## Security Considerations

### MVP Risks

The embedded key approach has known risks:

1. **Key Extraction**: Attackers can decompile the app and extract the key
2. **Unauthorized Usage**: Extracted key can be used for personal projects
3. **Cost Exposure**: You pay for unauthorized API usage

### Mitigations

1. **OpenAI Billing Limits**
   - Set a monthly spending limit on your OpenAI account
   - Configure usage alerts at 50%, 75%, 90% of limit
   - Example: Limit of $100/month, alerts at $50, $75, $90

2. **Usage Monitoring**
   - Check OpenAI dashboard weekly for unusual patterns
   - Look for: spikes in usage, requests from unusual sources
   - Set up programmatic monitoring if available

3. **Code Obfuscation**
   - Use ConfuserEx or similar on release builds
   - Obfuscate method names, control flow, strings
   - Makes extraction harder (but not impossible)

4. **Key Rotation Plan**
   - If key is compromised, have a plan to:
     - Revoke the key on OpenAI
     - Generate a new key
     - Push app update with new obfuscated key
     - Accept ~24-48 hour window where Pro users can't use managed key

### Future: Server Proxy

When you're ready to migrate, create `ServerProxyManagedKeyProvider`:

```csharp
public class ServerProxyManagedKeyProvider : IManagedKeyProvider
{
    private readonly HttpClient _httpClient;
    private readonly ISubscriptionService _subscriptionService;
    
    public string ProviderId => "proxy";
    public string ModelId => "gpt-4o-mini";
    
    public async Task<string?> GetApiKeyAsync()
    {
        // Instead of returning a key, this provider would:
        // 1. Get subscription receipt
        // 2. Send to your server for validation
        // 3. Server proxies the actual API call
        // 4. Return result
        
        // Or, server could return a short-lived token
        throw new NotImplementedException("Server proxy not yet implemented");
    }
}
```

## File Changes Summary

| File | Action | Description |
|------|--------|-------------|
| `src/FocusBot.Core/Interfaces/IManagedKeyProvider.cs` | Create | Interface for managed key access |
| `src/FocusBot.Infrastructure/Services/EmbeddedManagedKeyProvider.cs` | Create | MVP obfuscated key implementation |
| `src/FocusBot.Infrastructure/Services/KeyObfuscationHelper.cs` | Create | Development helper for key obfuscation |
| `src/FocusBot.Infrastructure/Services/LlmService.cs` | Modify | Use managed key for subscribed users |
| `src/FocusBot.App/App.xaml.cs` | Modify | Register managed key provider |
| `src/FocusBot.App/Views/SettingsPage.xaml` | Modify | Remove "coming soon" for subscribed users |

## Setup Instructions

### 1. Generate Obfuscated Key Data

In a debug build, run:

```csharp
#if DEBUG
KeyObfuscationHelper.GenerateObfuscatedKey("sk-proj-your-actual-key-here");
#endif
```

### 2. Copy Output to Provider

Take the generated byte arrays and paste them into `EmbeddedManagedKeyProvider.cs`:

```csharp
var part1 = Deobfuscate(
    new byte[] { 0x29, 0x57, 0x13, ... }, // Generated output
    GetXorKey1());
```

### 3. Remove Key from Source

- Delete the actual key from your code/notes
- The obfuscated bytes are now the only representation
- Commit only the obfuscated version

### 4. Apply Code Obfuscation

For release builds, apply additional obfuscation:

```xml
<!-- In .csproj for release builds -->
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <DebugType>none</DebugType>
    <DebugSymbols>false</DebugSymbols>
</PropertyGroup>
```

Consider using ConfuserEx or Dotfuscator for additional protection.

## Test Criteria

### Manual Testing

1. **Managed Mode + Subscribed**
   - Select "FocusBot Pro" mode
   - With active subscription → Classifications should work
   - Focus overlay should show correct colors
   - No API key configuration needed

2. **Managed Mode + Not Subscribed**
   - Select "FocusBot Pro" mode
   - Without subscription → Shows "Please subscribe" message
   - Classification returns error (not default score)

3. **Own Key Mode (Regression)**
   - Select "Own Key" mode
   - With valid API key → Works exactly as before
   - Without API key → Existing behavior (score defaults to 5)

4. **Mode Switching**
   - Switch from Own to Managed while subscribed → Works immediately
   - Switch from Managed to Own → Works with user's key

5. **Error Handling**
   - Managed key API error → Shows user-friendly error
   - Network error → Graceful degradation

### Unit Tests

```csharp
[Fact]
public async Task ClassifyAlignmentAsync_UsesManagedKey_WhenSubscribed()
{
    // Arrange
    var settingsMock = new Mock<ISettingsService>();
    settingsMock.Setup(s => s.GetApiKeyModeAsync())
                .ReturnsAsync(ApiKeyMode.Managed);
    
    var subMock = new Mock<ISubscriptionService>();
    subMock.Setup(s => s.IsSubscribedAsync()).ReturnsAsync(true);
    
    var keyMock = new Mock<IManagedKeyProvider>();
    keyMock.Setup(k => k.GetApiKeyAsync())
           .ReturnsAsync("sk-test-key");
    keyMock.Setup(k => k.ProviderId).Returns("openai");
    keyMock.Setup(k => k.ModelId).Returns("gpt-4o-mini");
    
    // ... setup API mock to capture the key used
    
    var service = new LlmService(settingsMock.Object, subMock.Object, 
                                  keyMock.Object, /* ... */);
    
    // Act
    var result = await service.ClassifyAlignmentAsync(
        "task", null, "process", "title");
    
    // Assert
    keyMock.Verify(k => k.GetApiKeyAsync(), Times.Once);
    // Verify API was called with managed key
}

[Fact]
public async Task EmbeddedManagedKeyProvider_ReturnsKey()
{
    var provider = new EmbeddedManagedKeyProvider();
    var key = await provider.GetApiKeyAsync();
    
    Assert.NotNull(key);
    Assert.StartsWith("sk-", key);
}
```

## Definition of Done

- [ ] `IManagedKeyProvider` interface created
- [ ] `EmbeddedManagedKeyProvider` implemented with obfuscation
- [ ] `KeyObfuscationHelper` created for development
- [ ] `LlmService` uses managed key for subscribed users
- [ ] Managed key provider registered in DI
- [ ] Settings UI updated (removed "coming soon")
- [ ] OpenAI billing limits configured
- [ ] Usage monitoring set up
- [ ] Unit tests pass
- [ ] End-to-end manual testing completed
- [ ] Code obfuscation applied to release build

## End-to-End Flow Complete

After Phase 3, the complete pricing flow works:

```
User selects "FocusBot Pro" → 
  App checks subscription →
    Not subscribed → "Subscribe Now" button →
      User purchases → Subscription active
    Subscribed → 
      App gets managed key →
      Classifications work automatically
```

Free users continue using their own API keys with no changes.

# Extension Presence Protocol

## Purpose

Lightweight local WebSocket connection between the Windows desktop app and browser extension to coordinate browser classification.

**Problem:** When both the desktop app and extension are running, the desktop app sees browser windows as just "msedge" or "chrome" without URL context, leading to inaccurate classifications. The extension has the actual URL and provides better classifications for browser activity.

**Solution:** Extension announces its presence to the desktop app via WebSocket ping/pong. When the extension is online, the desktop app skips browser process classification and lets the extension handle it.

---

## Protocol Spec

### Server (Windows Desktop App)

- **Endpoint:** `ws://localhost:9876/foqus-presence` (primary)
- **Backup:** `ws://localhost:9877/foqus-presence`
- **Server:** `ExtensionPresenceService` in `FocusBot.Infrastructure`
- **Accept:** Single WebSocket connection (new connection closes previous)
- **Timeout:** 60 seconds (if no ping received, extension is considered offline)

### Client (Browser Extension)

- **Connect:** On extension startup (service worker wake)
- **Reconnect:** Every 5 seconds if disconnected
- **Port fallback:** Try primary (9876), then backup (9877)
- **Ping interval:** 30 seconds

### Messages

**Extension → Desktop: Ping**
```json
{ "type": "ping" }
```

**Desktop → Extension: Pong** (optional)
```json
{ "type": "pong" }
```

---

## Desktop App Behavior

```csharp
if (IsBrowserProcess(processName) && extensionPresence.IsExtensionOnline)
{
    // Skip desktop classification - extension POSTs /classify on URL/title change.
    // If the last ClassificationChanged (SignalR) was from the extension, restore that
    // score/reason when the OS window title still matches the snapshot taken at hub delivery.
    // Otherwise show neutral Unclear until the next hub update (refocus alone may not classify).
    return;
}

// Otherwise, classify as normal
await ClassifyAsync(processName, windowTitle, ...);
```

**Extension online:** Desktop app skips msedge, chrome, firefox, brave, opera (no duplicate classify from Windows). The focus bar replays the last extension hub classification when the foreground window title matches; otherwise it shows neutral Unclear until the next hub broadcast.

**Extension offline:** Desktop app classifies all foreground windows including browsers

---

## Failure Modes

| Scenario | Behavior |
|----------|----------|
| Extension not installed | Desktop app classifies browsers (fallback) |
| WebSocket port in use | Tries backup port 9877; if both fail, desktop classifies browsers |
| Extension crashes | Desktop app detects timeout after 60s, resumes browser classification |
| Desktop app not running | Extension reconnects every 5s; no impact on extension functionality |

---

## Implementation Files

**Desktop App:**
- `src/FocusBot.Infrastructure/Services/ExtensionPresenceService.cs`
- `src/FocusBot.Infrastructure/Services/FocusSessionOrchestrator.cs` (checks `IsExtensionOnline`)
- `src/FocusBot.App/App.xaml.cs` (starts service on launch)

**Browser Extension:**
- `browser-extension/src/shared/extensionPresence.ts`
- `browser-extension/src/background/index.ts` (starts on install/startup)

---

## Why Not Use SignalR?

SignalR already syncs classifications across devices, but it doesn't prevent duplicate requests:

- **With WebSocket presence:** Desktop app knows extension is running → skips browser classification → 1 request
- **Without presence:** Both send requests → coalescing picks one → 2 requests (wastes 1)

Local WebSocket also works offline and has no cloud dependency.

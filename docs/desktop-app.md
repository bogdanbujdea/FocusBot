# Desktop App

The Foqus Windows desktop app is built with WinUI 3 and .NET 10. It monitors foreground windows, classifies activity alignment, and provides a floating overlay for quick focus control.

---

## Project Structure

| Project | Purpose |
|---|---|
| `FocusBot.App` | WinUI 3 UI, XAML views, DI wiring, Win32 overlay |
| `FocusBot.App.ViewModels` | Presentation logic (CommunityToolkit.Mvvm) |
| `FocusBot.Infrastructure` | Data access, Win32 services, auth, LLM providers |
| `FocusBot.Core` | Domain entities and interfaces |

---

## Pages

### FocusPage

Main board view showing the active task, focus score, session timeline, and recent activity.

### SettingsPage

User settings including account management, subscription info, API key configuration (BYOK), and overlay preferences.

#### Plan Selection Visibility

The Settings page shows plan selection options based on authentication and subscription state:

| User State | Plan Selection UI |
|---|---|
| **Not logged in** | Hidden — user must sign in first |
| **Trial (TrialFullAccess)** | Plan cards with features: BYOK ("You provide your own API key") and Premium ("Platform-managed AI — no API key needed") |
| **BYOK (CloudBYOK)** | Upgrade card for Foqus Premium |
| **Premium (CloudManaged)** | Hidden — user is on the highest tier |

Relevant ViewModel properties:
- `ShowPlanOptions` — true when signed in AND on trial
- `ShowUpgradeToCloudManaged` — true when signed in AND on BYOK plan
- `PlanEndDateLabel` — shows "Expires at: Apr 5, 11:59 AM" for trial, "Renews: Apr 5, 11:59 AM" for paid plans

All plan selection buttons open the billing page at `https://app.foqus.me/billing`.

---

## ViewModels

### PlanSelectionViewModel

Manages plan display and tier selection. Key properties:

| Property | Purpose |
|---|---|
| `CurrentPlan` | The user's current `PlanType` |
| `IsSignedIn` | Whether the user is authenticated |
| `PlanDisplayName` | Human-readable plan name |
| `PlanEndDateLabel` | Expiration or renewal date (e.g., "Expires at: Apr 5, 11:59 AM") |
| `ShowPlanOptions` | Show BYOK + Premium plan cards (trial users) |
| `ShowUpgradeToCloudManaged` | Show upgrade button (BYOK users) |
| `IsCloudBYOKPlan` | True when on BYOK plan (controls API key section visibility) |

### AccountSettingsViewModel

Handles magic-link authentication and sign-out.

### ApiKeySettingsViewModel

Manages BYOK API key storage (DPAPI encrypted) and provider/model selection.

### OverlaySettingsViewModel

Controls the floating focus overlay visibility preference.

---

## Services

See [docs/platform-overview.md](platform-overview.md) for architecture details and [docs/integration.md](integration.md) for cross-device sync via SignalR.

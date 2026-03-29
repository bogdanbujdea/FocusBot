# Phase 2: Desktop App (WinUI 3) — Trial Dialog, Banner, and BYOK Prompt

**Depends on:** Phase 1 (API auto-trial + `TrialFullAccess` plan type).

**Goal:** When a user signs in on the Windows desktop app, they see a welcome dialog explaining the 24h trial and what to expect. A persistent trial banner with a countdown appears at the top of the focus page. After the trial expires (or after subscribing to Cloud BYOK), the user is prompted to enter their API key.

**After this phase:** Desktop users get the full trial experience without needing to visit the web app. The web app handles plan selection and Paddle checkout; the desktop app links to it when needed.

---

## Existing infrastructure to leverage

- **`ContentDialog` pattern:** [`HowItWorksDialog.xaml`](../src/FocusBot.App/Views/HowItWorksDialog.xaml) — first-run dialog with primary/secondary buttons, shown via `ShowHowItWorksDialogAsync()` in [`FocusPage.xaml.cs`](../src/FocusBot.App/Views/FocusPage.xaml.cs) with a "seen" flag.
- **`InfoBar` pattern:** [`FocusPage.xaml`](../src/FocusBot.App/Views/FocusPage.xaml) — inline dismissible alert for API errors, bound to `ViewModel.IsApiErrorVisible`.
- **`TrialEndTimeConverter`:** [`TrialEndTimeConverter.cs`](../src/FocusBot.App/Converters/TrialEndTimeConverter.cs) — already registered in `App.xaml` but not used in any XAML. Ready to wire.
- **Plan service:** [`PlanService.cs`](../src/FocusBot.Infrastructure/Services/PlanService.cs) — caches subscription status from `GET /subscriptions/status`, raises `PlanChanged` event. Refreshes on SignalR `PlanChanged` notification.
- **Encrypted key storage:** [`SettingsService.cs`](../src/FocusBot.Infrastructure/Services/SettingsService.cs) — API key encrypted with `IDataProtector` (Windows Data Protection / DPAPI) in `settings.json`. Keys stored under `ApplicationData.Current.LocalFolder.Path/keys/`.
- **API key UI:** [`SettingsPage.xaml`](../src/FocusBot.App/Views/SettingsPage.xaml) + [`ApiKeySettingsViewModel.cs`](../src/FocusBot.App.ViewModels/ApiKeySettingsViewModel.cs) — existing text field for entering the OpenAI key.
- **Unused ViewModel properties:** `ShowUpgradeCta` and `ShowSignInPrompt` on [`PlanSelectionViewModel.cs`](../src/FocusBot.App.ViewModels/PlanSelectionViewModel.cs) — defined but not bound in any XAML. Can be wired for post-trial states.

---

## Trial welcome dialog

### New XAML

**New: `src/FocusBot.App/Views/TrialWelcomeDialog.xaml` (+ `.xaml.cs`)**

- `ContentDialog` following the `HowItWorksDialog` pattern.
- Content:
  - Short Foqus intro (what the app does, what they can try).
  - "You have 24 hours of full access to all features."
  - "After the trial, choose a plan on the billing page to continue."
- Primary button: "Got it" (dismiss).
- Secondary button: "View Plans" (opens `app.foqus.me/billing` in the default browser).

### Trigger

**In [`FocusPage.xaml.cs`](../src/FocusBot.App/Views/FocusPage.xaml.cs):**

- After sign-in, when `PlanService` reports `Status == Trial` and `PlanType == TrialFullAccess`:
  - Check `ApplicationData.Current.LocalSettings` for `TrialWelcomeSeen` flag.
  - If not set: show `TrialWelcomeDialog` via `ShowAsync()`.
  - After dialog closes: set `TrialWelcomeSeen = true` in local settings.
- This runs after the existing `HowItWorksDialog` check (sequential, not overlapping).

---

## Trial banner on FocusPage

### XAML

**In [`FocusPage.xaml`](../src/FocusBot.App/Views/FocusPage.xaml):**

- Add an `InfoBar` (or styled `Border`) at the top of the page, above existing content.
- Bind visibility to ViewModel property `IsTrialActive`.
- Bind trial end time using the existing `TrialEndTimeConverter`.
- Include a "Manage Plan" `HyperlinkButton` that opens the web billing URL.

### ViewModel

**In [`FocusPageViewModel.cs`](../src/FocusBot.App.ViewModels/FocusPageViewModel.cs) or [`PlanSelectionViewModel.cs`](../src/FocusBot.App.ViewModels/PlanSelectionViewModel.cs):**

- Expose `IsTrialActive` (bool): `true` when `Status == Trial && TrialEndsAtUtc > UtcNow`.
- Expose `TrialEndsAt` (DateTime?): from cached subscription status via `IPlanService`.
- Update these properties when `PlanChanged` fires (already wired via SignalR).
- Wire the unused `ShowUpgradeCta` for the post-trial expired state (trial expired, no paid subscription).

---

## BYOK API key prompt

### When to prompt

After a Paddle webhook confirms the user subscribed to `CloudBYOK`:

1. SignalR `PlanChanged` fires.
2. Desktop refreshes plan via `PlanService.RefreshAsync()`.
3. If `PlanType == CloudBYOK` and no API key is stored (`GetApiKeyAsync()` returns null/empty): show a prompt.

### Prompt UI

- Use `ContentDialog` or `InfoBar` directing the user to the Settings page to enter their key.
- Include security messaging: **"Your API key is encrypted on this device using Windows Data Protection (DPAPI). It never leaves your machine unencrypted — it is sent to the AI provider over HTTPS with each classification request and is not stored on our servers."**

### Existing flow (no changes needed)

- [`SettingsPage.xaml`](../src/FocusBot.App/Views/SettingsPage.xaml) already has the API key input field.
- [`ApiKeySettingsViewModel`](../src/FocusBot.App.ViewModels/ApiKeySettingsViewModel.cs) saves via `ISettingsService.SetApiKeyAsync()`.
- [`SettingsService`](../src/FocusBot.Infrastructure/Services/SettingsService.cs) encrypts with `_protector.Protect()` and stores in `settings.json`.
- [`AlignmentClassificationService`](../src/FocusBot.Infrastructure/Services/AlignmentClassificationService.cs) reads the key and sends it as `X-Api-Key` header on `POST /classify`.

---

## Tests

### ViewModel tests

**In [`FocusBot.App.ViewModels.Tests`](../tests/FocusBot.App.ViewModels.Tests/):**

- `IsTrialActive` returns `true` when plan status is `Trial` with future `TrialEndsAtUtc`.
- `IsTrialActive` returns `false` when `TrialEndsAtUtc` is in the past.
- `ShowUpgradeCta` is `true` when trial has expired and no active subscription.
- `PlanChanged` event updates trial properties.

### Integration considerations

- Verify the `TrialEndTimeConverter` output format with sample dates.
- Verify the `HowItWorksDialog` → `TrialWelcomeDialog` sequencing does not show both simultaneously.

---

## Documentation

- [`docs/paddle-implementation-summary.md`](paddle-implementation-summary.md): Add desktop trial dialog and banner behavior. Document BYOK prompt trigger and security model.
- [`AGENTS.md`](../AGENTS.md): Update desktop app section with trial dialog and banner details.

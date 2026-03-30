# Phase 3: Browser Extension — Trial Banner, Welcome, and BYOK Prompt

**Depends on:** Phase 1 — **complete** (API + web app; see [phase-1-web-app-and-api.md](phase-1-web-app-and-api.md), [web-app-sign-in-and-trials.md](../web-app-sign-in-and-trials.md)).

**Goal:** When a user signs in via the browser extension, they see a compact trial banner in the popup explaining the 24h trial and linking to billing. After subscribing to Cloud BYOK, the options page highlights the API key field if empty and displays security messaging about how the key is stored.

**After this phase:** All three clients (web app, desktop, extension) provide the full trial experience. Plan selection and checkout remain centralized on the web app.

---

## Existing infrastructure to leverage

- **"Attention" card pattern:** [`AppShell.tsx`](../browser-extension/src/ui/AppShell.tsx) — inline card with title, message, and dismiss button when `state.lastError` is set. Reuse this pattern for the trial banner.
- **Options page plan UI:** [`options/main.tsx`](../browser-extension/src/options/main.tsx) — plan radio cards with "Current plan" pill, "Open billing" links to `getWebAppBillingUrl()`.
- **Subscription status API:** [`apiClient.ts`](../browser-extension/src/shared/apiClient.ts) — `getSubscriptionStatus()` returns `SubscriptionStatusResponse` including `trialEndsAt`.
- **Plan refresh:** [`background/index.ts`](../browser-extension/src/background/index.ts) — handles `REFRESH_PLAN` message, maps API `planType` to stored `settings.plan`.
- **Web app billing URL:** [`webAppUrl.ts`](../browser-extension/src/shared/webAppUrl.ts) — `getWebAppBillingUrl()` helper.
- **API key storage:** [`storage.ts`](../browser-extension/src/shared/storage.ts) — `loadSettings` / `saveSettings` with `openAiApiKey` in `chrome.storage.local`. Currently stored in plaintext.
- **API key field:** [`options/main.tsx`](../browser-extension/src/options/main.tsx) — existing OpenAI API key text input for BYOK plans.

---

## Trial banner in popup

### In [`AppShell.tsx`](../browser-extension/src/ui/AppShell.tsx) (popup only)

- Gate on Foqus trial semantics, not status string alone: show only when `status === "trial"` **and** `planType === 0` (TrialFullAccess) and `trialEndsAt` is in the future.
- Render a **single-row compact banner** to minimize vertical space in the popup.
- Content should fit one row: `Trial ends ...` + `Manage plan` link (opens `getWebAppBillingUrl()`).
- No dismiss button required; it disappears automatically when the trial ends or the user subscribes.
- Do not render this banner in the sidepanel.

### State source

- The extension already fetches subscription status via `getSubscriptionStatus()`. Persist `trialEndsAt`, `status`, and `planType` in shared settings/state so popup UI can apply Foqus-trial-only gating.
- On `REFRESH_PLAN`, persist those fields from API response together.

---

## Subscription summary in options page

### In [`options/main.tsx`](../browser-extension/src/options/main.tsx)

- Replace plan card comparison UI with a simple summary, aligned with desktop settings:
  - Current plan
  - End date (`trialEndsAt` for trial, billing period end for active/canceled if available)
  - `Manage subscription` button (opens web billing URL)
- Keep a lightweight `Refresh` action and status/error text.
- Keep sign-in prompt when unauthenticated.

---

## BYOK API key prompt

### When to prompt

After the user subscribes to Cloud BYOK (detected via plan refresh from the API):

- If `settings.plan === 'cloud-byok'` and `settings.openAiApiKey` is empty: highlight the API key section in options and show an inline message.

### Options page enhancement

**In [`options/main.tsx`](../browser-extension/src/options/main.tsx):**

- When the condition above is true: show an alert/card near the API key field: "Enter your API key to start using Foqus with your own AI provider."
- Include security messaging:
  - **"Your API key is stored in Chrome's protected extension storage, isolated from other extensions and websites. It is sent directly to the AI provider over HTTPS and is never transmitted to Foqus servers."**

### Classification flow (existing, no changes needed)

The extension already has two classification paths in [`classifier.ts`](../browser-extension/src/shared/classifier.ts):

1. **BYOK (direct):** Calls `https://api.openai.com/v1/chat/completions` with `Authorization: Bearer ${settings.openAiApiKey}`. The key goes directly to OpenAI, not through the Foqus API.
2. **Cloud Managed:** Calls `POST /classify` via [`apiClient.ts`](../browser-extension/src/shared/apiClient.ts) with JWT only; the server uses its managed key.

No changes to the classification flow are needed. The BYOK prompt just ensures users enter their key so path 1 works.

### Security note

`chrome.storage.local` is sandboxed per-extension (not accessible to other extensions or web pages) but is not encrypted at rest on disk. True at-rest encryption would require the Web Crypto API, adding significant complexity. For this phase, accurate messaging about the actual security model is the priority. Disk encryption via Web Crypto can be a future hardening task.

---

## Plan/status mapping updates

### In [`options/main.tsx`](../browser-extension/src/options/main.tsx)

- Remove legacy "Free (BYOK)" naming in UI copy.
- Show `planType = 0` as **Trial** in extension display labels.
- After trial expiry with no paid subscription: show "No active plan" with a billing link.

---

## Tests

### Extension tests

- **AppShell popup trial banner:** renders only when `status = trial` + `planType = 0` + future `trialEndsAt`; hidden for paid plans and sidepanel.
- **Popup compact layout:** trial banner fits in one row and does not increase popup height significantly.
- **Options subscription summary:** shows current plan, end date, and billing button without plan cards.
- **BYOK prompt:** shown when plan is `cloud-byok` and key is empty; hidden when key is present.
- **Plan labels:** `TrialFullAccess` (0) maps to "Trial" display name.

---

## Documentation

- [`docs/paddle-implementation-summary.md`](paddle-implementation-summary.md): Add browser extension trial banner and BYOK prompt behavior. Document the extension's dual classification paths and security model for stored keys.
- [`AGENTS.md`](../AGENTS.md): Update browser extension section with trial banner and BYOK key prompt details.

# Phase 3: Browser Extension — Trial Banner, Welcome, and BYOK Prompt

**Depends on:** Phase 1 (API auto-trial + `TrialFullAccess` plan type).

**Goal:** When a user signs in via the browser extension, they see a welcome message explaining the 24h trial and a countdown banner in the popup/sidepanel. After subscribing to Cloud BYOK, the options page highlights the API key field if empty and displays security messaging about how the key is stored.

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

## Trial banner in popup / sidepanel

### In [`AppShell.tsx`](../browser-extension/src/ui/AppShell.tsx)

- When subscription status is `trial` and `trialEndsAt` is in the future: render a banner card (similar to the existing "Attention" card pattern but with an informational style, not error).
- Content: "Trial — [countdown or end time]" + link to billing via `getWebAppBillingUrl()`.
- The banner should be visible in both popup and sidepanel views (both use `AppShell`).
- Dismiss is optional — the banner naturally disappears when the trial expires or the user subscribes.

### State source

- The extension already fetches subscription status via `getSubscriptionStatus()`. Store `trialEndsAt` in the shared settings/state so `AppShell` can read it.
- On `REFRESH_PLAN`, also persist `trialEndsAt` and `status` from the API response.

---

## Welcome section in options page

### In [`options/main.tsx`](../browser-extension/src/options/main.tsx)

- When `status === 'trial'` and a `chrome.storage.local` key `trialWelcomeSeen.<userId>` is not set: show a welcome section above the plan cards.
- Content: short Foqus intro, "You have 24 hours of full access," link to billing to compare plans.
- Dismiss button sets `trialWelcomeSeen.<userId>` in `chrome.storage.local`.

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

## Plan card updates

### In [`options/main.tsx`](../browser-extension/src/options/main.tsx)

- Remove or rename the "Free (BYOK)" plan card to match the Phase 1 changes (no free tier).
- Update plan labels: show `TrialFullAccess` (plan type 3) as "Trial" in `PLAN_LABELS`.
- After trial expiry with no subscription: show "No active plan" with a link to the web billing page.

---

## Tests

### Extension tests

- **AppShell trial banner:** renders when status is `trial` with future `trialEndsAt`; hidden when status is `active`.
- **Options welcome section:** visible on trial when not dismissed; hidden after dismiss; localStorage flag persisted.
- **BYOK prompt:** shown when plan is `cloud-byok` and key is empty; hidden when key is present.
- **Plan labels:** `TrialFullAccess` (3) maps to "Trial" display name.

---

## Documentation

- [`docs/paddle-implementation-summary.md`](paddle-implementation-summary.md): Add browser extension trial banner and BYOK prompt behavior. Document the extension's dual classification paths and security model for stored keys.
- [`AGENTS.md`](../AGENTS.md): Update browser extension section with trial banner and BYOK key prompt details.

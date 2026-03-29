# Phase 1: Web App + API — Generic 24h Trial

**Goal:** Remove the "Free (BYOK)" tier. Auto-start a 24h full-access trial server-side on first sign-in. Show a welcome modal and countdown banner in the web app. Rework the billing page so users choose Cloud BYOK or Cloud Managed after (or during) the trial.

**After this phase:** A user signs in to `app.foqus.me`, immediately gets a 24h trial, sees a welcome modal explaining Foqus + the trial, and a persistent countdown banner. The billing page shows only the two paid plans. Desktop and extension users who call `GET /subscriptions/status` also get the trial row created automatically, but UI changes for those platforms are in phases 2 and 3.

---

## API changes

### New plan type

Add `PlanType.TrialFullAccess = 3` to [`Subscription.cs`](../src/FocusBot.WebAPI/Data/Entities/Subscription.cs) to represent "trial, plan not yet chosen." This avoids storing a misleading `CloudBYOK` or `CloudManaged` value during the trial.

### EF migration

Add a migration for the new enum value. The column is an integer so no schema change is needed — just a new valid value.

### Auto-trial on first status check

In [`SubscriptionService.cs`](../src/FocusBot.WebAPI/Features/Subscriptions/SubscriptionService.cs) `GetStatusAsync`:

- When `subscription is null`: create a new `Subscription` row with `Status = Trial`, `PlanType = TrialFullAccess`, `TrialEndsAtUtc = UtcNow + 24h`.
- Return the `Trial` status with `trialEndsAt`.
- Second and subsequent calls return the existing row (idempotent).

### Update `POST /subscriptions/trial`

In [`SubscriptionEndpoints.cs`](../src/FocusBot.WebAPI/Features/Subscriptions/SubscriptionEndpoints.cs):

- Keep for explicit activation but also accept empty body or `TrialFullAccess`. Returns 409 if a row already exists (unchanged).

### Classification gate

[`ClassificationEndpoints.cs`](../src/FocusBot.WebAPI/Features/Classification/ClassificationEndpoints.cs) — `IsSubscribedOrTrialActiveAsync` already grants access for `Trial` status with valid `TrialEndsAtUtc`. The new `TrialFullAccess` plan type does not affect access logic. No change needed.

---

## Web app changes

### SubscriptionProvider (new context)

**New file: `src/foqus-web-app/src/contexts/SubscriptionContext.tsx`**

- On authenticated session: call `GET /subscriptions/status`.
- Expose `{ subscription, loading, error, refresh }` via React context.
- Wrap inside `Layout` or `ProtectedRoute` in [`App.tsx`](../src/foqus-web-app/src/App.tsx).
- All pages that need subscription state consume this context instead of fetching independently.

### Welcome modal (new component)

**New file: `src/foqus-web-app/src/components/TrialWelcomeModal.tsx`**

- Shown when `status === 'trial'` AND `localStorage` key `foqus.trialWelcomeSeen.<userId>` is not set.
- Content: short Foqus intro, "You have 24 hours of full access to explore," link to `/billing` to compare plans.
- On dismiss: set localStorage flag so it does not reappear.
- Accessibility: focus trap, `role="dialog"`, `aria-labelledby`, ESC to close, primary dismiss CTA.
- Rendered in `Layout.tsx`, reads from `SubscriptionContext`.

### Trial countdown banner

**In [`Layout.tsx`](../src/foqus-web-app/src/components/Layout.tsx):**

- When `status === 'trial'` and `trialEndsAt` is in the future: show a fixed banner above main content.
- Live countdown or formatted end time. Link to `/billing`.
- Style in [`Layout.css`](../src/foqus-web-app/src/components/Layout.css) to match the existing dark/purple theme.

### Billing page cleanup

**In [`BillingPage.tsx`](../src/foqus-web-app/src/pages/BillingPage.tsx):**

- **Remove** the static "Free (BYOK)" `PlanCard` and associated logic (lines 204-216 and the `FreeBYOK` current-plan check).
- **`none` status (should rarely happen now):** Show "No active subscription" header with a neutral badge.
- **`trial` status:** Generic header "Trial — Full Access", show trial end time. No plan-specific copy until they subscribe.
- **`expired` / `canceled`:** Clear CTAs to pick a paid plan.
- **`active`:** Unchanged (portal, period dates, manage subscription).
- Optionally consume `SubscriptionContext` to avoid a duplicate `getSubscriptionStatus` fetch.

### Type changes

**In [`types.ts`](../src/foqus-web-app/src/api/types.ts):**

- Add `TrialFullAccess: 3` to the `PlanType` const.
- Update `getPlanDisplayName`: map `3` to `"Trial"`.
- Remove or deprecate `FreeBYOK: 0` display name (no longer user-facing).

---

## Tests

### API unit tests

**In [`SubscriptionServiceTests.cs`](../tests/FocusBot.WebAPI.Tests/Features/Subscriptions/SubscriptionServiceTests.cs):**

- `GetStatusAsync` auto-creates trial for new user (no existing row).
- Second `GetStatusAsync` call returns the same trial row (idempotent, does not create a duplicate).
- `TrialFullAccess` plan type works with `IsSubscribedOrTrialActiveAsync` (returns `true` during trial, `false` after expiry).

### API integration tests

**In [`SubscriptionTests.cs`](../tests/FocusBot.WebAPI.IntegrationTests/SubscriptionTests.cs):**

- First `GET /subscriptions/status` returns `trial` status with `trialEndsAt` set.
- Subsequent calls return the same trial.
- `POST /subscriptions/trial` returns 409 after auto-trial was created.

### Web app tests

**In [`BillingPage.test.tsx`](../src/foqus-web-app/src/pages/BillingPage.test.tsx):**

- Remove assertions for "Free (BYOK)" card.
- Add: trial status shows generic trial header, no free card rendered.
- Add: paid plan cards still render from Paddle pricing mock.

**New tests for `SubscriptionProvider`:** first render fetches status; context exposes subscription to children.

**New tests for `TrialWelcomeModal`:** shows on trial status when not dismissed; dismisses on button click; does not show after localStorage flag is set.

**New tests for trial banner:** visible during trial; hidden when status is active; countdown or end time displayed.

---

## Documentation

- [`docs/paddle-implementation-summary.md`](paddle-implementation-summary.md): Document auto-trial on first status check, `TrialFullAccess` plan type, remove "Free (BYOK) static card" references, add trial modal/banner behavior for web app.
- [`src/foqus-web-app/README.md`](../src/foqus-web-app/README.md): Mention trial welcome modal, global trial banner, and SubscriptionContext.
- [`AGENTS.md`](../AGENTS.md): Update trial activation description — auto on first status check, not explicit `POST`.

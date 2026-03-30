# Phase 1: Web App + API — Generic 24h Trial

**Status: complete** (web app + API behavior shipped; see [web-app-sign-in-and-trials.md](../web-app-sign-in-and-trials.md) for the current sign-in and subscription flow).

**Goal (achieved):** Remove the "Free (BYOK)" tier. Auto-start a 24h full-access trial server-side on first sign-in. Show a welcome modal and countdown banner in the web app for the **Foqus trial only**. Rework the billing page so users choose Cloud BYOK or Cloud Managed after (or during) the trial.

**After this phase:** A user signs in to `app.foqus.me`, calls **`GET /auth/me`** (provisions user + trial), loads subscription via context; sees welcome modal + banner while on **`PlanType.TrialFullAccess`**. The billing page shows the two paid plans from Paddle. Desktop/extension still use their own UI in later phases.

---

## Delivered (implementation summary)

| Area | Notes |
|------|--------|
| **Plan type** | `PlanType.TrialFullAccess = **0**` (not 3 — original backlog draft had the wrong value). `CloudBYOK = 1`, `CloudManaged = 2`. |
| **Provisioning** | **`GET /auth/me`** creates `Users` + initial subscription/trial. Web app uses [`subscriptionBootstrap.ts`](../../src/foqus-web-app/src/contexts/subscriptionBootstrap.ts): **`getMe()`** then **`getSubscriptionStatus()`**, single-flight per user id. |
| **`GET /subscriptions/status`** | Returns **403** if JWT is valid but no `Users` row (must call **`/auth/me`** first). May still create a defensive trial if user exists without a subscription row. |
| **Trial UI** | Banner + welcome modal gated on **`planType === TrialFullAccess`**, not `status === "trial"` alone (Paddle **`trialing`** also maps to the same API `trial` string). See [`Layout.tsx`](../../src/foqus-web-app/src/components/Layout.tsx). |
| **Billing** | No static Free BYOK card; context-based loading. |
| **Docs** | [web-app-sign-in-and-trials.md](../web-app-sign-in-and-trials.md), [paddle-guide.md](../paddle-guide.md), [AGENTS.md](../../AGENTS.md). |

---

## Original backlog spec (historical)

The sections below describe the original plan. Some line references and enum values differ from the final code — use the repository and [web-app-sign-in-and-trials.md](../web-app-sign-in-and-trials.md) as source of truth.

---

## API changes

### New plan type

Add `PlanType.TrialFullAccess = 0` to [`Subscription.cs`](../../src/FocusBot.WebAPI/Data/Entities/Subscription.cs) to represent "trial, plan not yet chosen." This avoids storing a misleading `CloudBYOK` or `CloudManaged` value during the trial.

### EF migration

Add a migration for the new enum value. The column is an integer so no schema change is needed — just a new valid value.

### Auto-trial on first status check

In [`SubscriptionService.cs`](../../src/FocusBot.WebAPI/Features/Subscriptions/SubscriptionService.cs) `GetStatusAsync`:

- When `subscription is null` **and** a `Users` row exists: create a new `Subscription` row with `Status = Trial`, `PlanType = TrialFullAccess`, `TrialEndsAtUtc = UtcNow + 24h`.
- Return the `Trial` status with `trialEndsAt`.
- Second and subsequent calls return the existing row (idempotent).

### Update `POST /subscriptions/trial`

In [`SubscriptionEndpoints.cs`](../../src/FocusBot.WebAPI/Features/Subscriptions/SubscriptionEndpoints.cs):

- Keep for explicit activation; accepts `TrialFullAccess`, `CloudBYOK`, or `CloudManaged`. Returns 409 if a row already exists.

### Classification gate

[`ClassificationEndpoints.cs`](../../src/FocusBot.WebAPI/Features/Classification/ClassificationEndpoints.cs) — `IsSubscribedOrTrialActiveAsync` already grants access for `Trial` status with valid `TrialEndsAtUtc`. The `TrialFullAccess` plan type does not break access logic.

---

## Web app changes

### SubscriptionProvider (new context)

**File: [`SubscriptionContext.tsx`](../../src/foqus-web-app/src/contexts/SubscriptionContext.tsx)** (+ [`subscriptionBootstrap.ts`](../../src/foqus-web-app/src/contexts/subscriptionBootstrap.ts))

- On authenticated **user id**: run bootstrap (`getMe` + `getSubscriptionStatus`).
- Expose `{ subscription, loading, error, refresh }` via React context.
- Wrapped inside `Layout` in [`Layout.tsx`](../../src/foqus-web-app/src/components/Layout.tsx).

### Welcome modal (new component)

**File: [`TrialWelcomeModal.tsx`](../../src/foqus-web-app/src/components/TrialWelcomeModal.tsx)**

- Shown when Foqus trial only (`planType` TrialFullAccess + trial semantics) AND `localStorage` key `foqus.trialWelcomeSeen.<userId>` is not set.
- Rendered in `Layout.tsx`, reads from `SubscriptionContext`.

### Trial countdown banner

**In [`Layout.tsx`](../../src/foqus-web-app/src/components/Layout.tsx):**

- Same gating as modal — Foqus trial only, not Paddle `trialing` on paid plan types.

### Billing page cleanup

**In [`BillingPage.tsx`](../../src/foqus-web-app/src/pages/BillingPage.tsx):**

- No static "Free (BYOK)" card.
- Consumes `SubscriptionContext` for subscription state.

### Type changes

**In [`types.ts`](../../src/foqus-web-app/src/api/types.ts):**

- `PlanType.TrialFullAccess: 0` in the `PlanType` const.

---

## Tests

### API unit tests

[`SubscriptionServiceTests.cs`](../../tests/FocusBot.WebAPI.Tests/Features/Subscriptions/SubscriptionServiceTests.cs) — coverage for `GetStatusAsync`, webhooks, provisioning guards.

### API integration tests

[`SubscriptionTests.cs`](../../tests/FocusBot.WebAPI.IntegrationTests/SubscriptionTests.cs) — `/auth/me` + `/subscriptions/status` flows.

### Web app tests

[`BillingPage.test.tsx`](../../src/foqus-web-app/src/pages/BillingPage.test.tsx) and related Vitest suites.

---

## Documentation (original checklist)

- [`docs/paddle-implementation-summary.md`](../paddle-implementation-summary.md)
- [`docs/web-app-sign-in-and-trials.md`](../web-app-sign-in-and-trials.md)
- [`src/foqus-web-app/README.md`](../../src/foqus-web-app/README.md)
- [`AGENTS.md`](../../AGENTS.md)

# Web app sign-in, provisioning, and trial status

This document describes how **foqus-web-app** authenticates with **Supabase**, provisions the app user in **FocusBot.WebAPI**, loads subscription state, and how **trial** semantics differ between the **Foqus 24h trial** and **Paddle** subscription states.

For Paddle checkout, webhooks, and pricing, see [paddle-guide.md](paddle-guide.md) and [paddle-implementation-summary.md](paddle-implementation-summary.md).

---

## End-to-end sign-in flow

```mermaid
sequenceDiagram
  participant User
  participant Supabase
  participant WebApp
  participant API as FocusBot.WebAPI
  participant DB as PostgreSQL

  User->>Supabase: Sign in (magic link / OAuth)
  Supabase-->>WebApp: Session JWT (sub = user id)
  WebApp->>API: GET /auth/me (Bearer)
  API->>DB: GetOrProvisionUserAsync
  Note over API,DB: Creates Users row if missing; creates Subscriptions trial row if missing
  API-->>WebApp: MeResponse (email, subscriptionStatus, planType)
  WebApp->>API: GET /subscriptions/status (Bearer)
  API-->>WebApp: SubscriptionStatusResponse
```

In practice the web app **bundles** the first two API calls for the shell:

1. **`GET /auth/me`** — Required first call after sign-in. It runs **`AuthService.GetOrProvisionUserAsync`**, which creates the **`Users`** row from JWT claims and, on first provisioning, creates a **`Subscriptions`** row for the **Foqus 24h trial** (`PlanType.TrialFullAccess`, `SubscriptionStatus.Trial`, `TrialEndsAtUtc`).

2. **`GET /subscriptions/status`** — Returns current subscription fields for the dashboard. **Must not** be used as the only call without provisioning: if there is no **`Users`** row, the API returns **403** with a message to call **`/auth/me`** first (see [SubscriptionService](../src/FocusBot.WebAPI/Features/Subscriptions/SubscriptionService.cs)).

Implementation: [`subscriptionBootstrap.ts`](../src/foqus-web-app/src/contexts/subscriptionBootstrap.ts) calls **`getMe()`** then **`getSubscriptionStatus()`** in a **single-flight** promise per Supabase user id so repeated session updates do not multiply identical requests. [`SubscriptionContext.tsx`](../src/foqus-web-app/src/contexts/SubscriptionContext.tsx) triggers this when the authenticated **`user.id`** is available.

---

## API responses clients should use

| Concept | Source | Notes |
|--------|--------|--------|
| **Identity** | Supabase JWT `sub` | Same as `Users.Id` / `Subscriptions.UserId` after provisioning. |
| **Provisioning** | `GET /auth/me` | Creates **`Users`** + initial subscription if needed. |
| **Plan tier** | `planType` (number) | `0` = TrialFullAccess (Foqus trial), `1` = CloudBYOK, `2` = CloudManaged. |
| **Lifecycle status** | `status` (string) | `trial`, `active`, `expired`, `canceled`, `none` (camelCase JSON). |

**Important:** The JSON field **`status: "trial"`** is used for both:

- The **Foqus app trial** (plan type **TrialFullAccess**), and  
- Paddle’s **`trialing`** subscription state, mapped in the API to the same enum value (`SubscriptionStatus.Trial`).

UI that should only reflect the **Foqus 24h trial** (banner, welcome modal) must also check **`planType === 0`** (TrialFullAccess). See [`Layout.tsx`](../src/foqus-web-app/src/components/Layout.tsx) (`isFoqusTrialOnly`).

---

## Foqus 24h trial (app-side)

- **Created** when the user is first provisioned via **`GET /auth/me`** (or defensively when **`GET /subscriptions/status`** runs if a **`Users`** row exists and no subscription row exists yet).
- **Plan type** in DB: **`PlanType.TrialFullAccess`** (0).
- **Not** the same as a Paddle price trial: do **not** configure a duplicate time-based trial on Paddle prices if you already use this app trial; see [AGENTS.md](../AGENTS.md) Paddle section.

---

## Paddle `trialing` and API `status`

**Webhooks** (`subscription.created`, `subscription.updated`) map Paddle subscription status to our **`SubscriptionStatus`** in [`MapSubscriptionStatus`](../src/FocusBot.WebAPI/Features/Subscriptions/SubscriptionService.cs):

| Paddle `status` | Stored `SubscriptionStatus` | JSON `status` (typical) |
|-----------------|-----------------------------|-------------------------|
| `active` | `Active` | `active` |
| `trialing` | `Trial` | `trial` |
| `past_due` | `Expired` | `expired` |
| `paused` | `Expired` | `expired` |
| `canceled` | `Canceled` | `canceled` |

So **`trialing`** from Paddle becomes **`status: "trial"`** in API responses, even when **`planType`** is already **CloudBYOK** or **CloudManaged** (paid product). After checkout, Paddle usually moves the subscription to **`active`**, which then maps to **`status: "active"`**.

**UI rule:** For “Foqus trial” messaging, use **`planType === TrialFullAccess (0)`**, not **`status === "trial"`** alone.

---

## Operational checklist

1. **First sign-in:** Ensure **`GET /auth/me`** runs before relying on subscription-only endpoints (web app bootstrap does this).
2. **Paddle prices:** Avoid a **second** trial period on the Paddle price if you rely on the **Foqus 24h trial** (`TrialFullAccess`), to avoid confusion between Paddle `trialing` and app trial semantics.
3. **Webhooks:** After purchase, **`subscription.updated`** / **`transaction.completed`** update **`Subscriptions`**; clients should refresh subscription state (web app uses **`SignalR` `PlanChanged`** on the focus hub where applicable).

---

## Related code

| Area | Location |
|------|----------|
| Auth + session | `foqus-web-app/src/auth/AuthProvider.tsx`, `supabase` client |
| Subscription bootstrap | `foqus-web-app/src/contexts/subscriptionBootstrap.ts`, `SubscriptionContext.tsx` |
| Provision user + trial | `FocusBot.WebAPI/Features/Auth/AuthService.cs` |
| Subscription status + webhooks | `FocusBot.WebAPI/Features/Subscriptions/SubscriptionService.cs` |

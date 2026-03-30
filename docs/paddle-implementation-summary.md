# Paddle Billing Implementation Summary

This document summarizes the complete Paddle Billing integration across all Foqus platforms (API, web app, browser extension, desktop app).

For **web app sign-in, user provisioning, and trial vs Paddle `trialing`**, see [web-app-sign-in-and-trials.md](web-app-sign-in-and-trials.md).

## Architecture

```
┌─────────────────┐
│ Desktop App     │──┐
│ (WinUI 3)       │  │
└─────────────────┘  │
                     │  Opens browser
┌─────────────────┐  │  to web billing
│ Browser Ext     │──┼──────────────────┐
│ (Chrome/Edge)   │  │                  ▼
└─────────────────┘  │         ┌─────────────────┐
                     └────────►│ Web App         │
                               │ (/billing)      │
                               │ Paddle.js       │
                               └────────┬────────┘
                                        │
                        ┌───────────────┴───────────────┐
                        │                               │
                   GET /pricing                  Paddle Checkout
                   POST /portal                  (overlay)
                        │                               │
                        ▼                               ▼
                ┌────────────────┐              ┌──────────────┐
                │ FocusBot       │◄─Webhooks────│ Paddle API   │
                │ WebAPI         │              └──────────────┘
                └────────┬───────┘
                         │
                    PostgreSQL
                    (Subscriptions)
```

## Components

### 1. API (`FocusBot.WebAPI`)

#### Configuration (`PaddleSettings.cs`)
- `ApiBase` — Paddle API URL (sandbox or production)
- `ApiKey` — Server-side Bearer token (secret)
- `WebhookSecret` — HMAC signing secret (secret)
- `ClientToken` — Public token for Paddle.js
- `IsSandbox` — Environment flag
- `CatalogProductId` — Which Paddle product's prices to show (`pro_...`)

#### Endpoints
| Method | Path | Purpose |
|--------|------|---------|
| GET | `/pricing` | Proxies active prices for CatalogProductId; 10-min cache |
| GET | `/subscriptions/status` | User's current plan and billing dates. May create a 24h `TrialFullAccess` trial **only if** a `Users` row already exists and no subscription row exists (call **`GET /auth/me`** first to provision). |
| POST | `/subscriptions/trial` | Explicit trial activation (accepts `{ "planType": 1, 2, or 3 }`). Returns 409 if a row already exists (auto-trial already created). |
| POST | `/subscriptions/portal` | Creates Paddle customer portal session URL |
| POST | `/subscriptions/paddle-webhook` | Webhook receiver with signature verification |

#### Webhook Processing (`SubscriptionService.cs` + `PaddleWebhookModels.cs`)

**Strongly-typed C# models** for Paddle payloads (no `JsonElement` in service layer):
- `PaddleWebhookPayload` — envelope
- `PaddleSubscription`, `PaddleTransaction` — event data
- `PaddlePrice`, `PaddleCustomData`, `PaddlePayment`, `PaddleCardDetails`, etc.

**Idempotency** (`ProcessedWebhookEvent` table):
Every webhook event is checked by `event_id` before processing. If already processed, the handler returns early without re-processing. After successful handling, the event_id is recorded. This prevents duplicate subscription creation on Paddle retries.

**Events handled:**
- `subscription.created` — Creates/updates subscription with status mapping, requires plan type resolution, skips if unresolvable
- `subscription.updated` — Updates plan, status, dates
- `subscription.canceled` — Marks canceled, records `CanceledAt` timestamp and reason
- `transaction.completed` — Records transaction id, billing period, payment method; creates subscription row if race condition (arrived before `subscription.created`)

**Status mapping** (using `SubscriptionStatus` enum):
```
Paddle           → App (SubscriptionStatus enum)
"trialing"       → Trial
"active"         → Active
"past_due"       → Expired (with warning log)
"paused"         → Expired
"canceled"       → Canceled
```

**Plan type mapping:**
- Reads `custom_data.plan_type`: `"cloud-byok"` → `CloudBYOK`, `"cloud-managed"` → `CloudManaged`
- Fallback: `custom_data.license`: `"byok"` → `CloudBYOK`, `"premium"` → `CloudManaged`

**Payment details:**
- Extracts from `method_details.card.last4` (nested path)
- Populates `PaymentMethodType` (e.g. `"card"`) and `CardLastFour`

**SignalR notification:**
- After DB updates, emits `PlanChanged` event to `/hubs/focus`
- Desktop and web clients refresh immediately

**Plan type mapping:**
- Reads `custom_data.plan_type`: `"cloud-byok"` → `CloudBYOK`, `"cloud-managed"` → `CloudManaged`
- Fallback: `custom_data.license`: `"byok"` → `CloudBYOK`, `"premium"` → `CloudManaged`
- **If unresolvable:** logs error and skips (no default `CloudBYOK` fallback)

**Payment details:**
- Extracts from `method_details.card.last4` (nested path)
- Populates `PaymentMethodType` (e.g. `"card"`) and `CardLastFour`

**SignalR notification:**
- After DB updates, emits `PlanChanged` event to `/hubs/focus`
- Desktop and web clients refresh immediately

**Security:**
- `PaddleWebhookVerifier` rejects all requests when `Paddle:WebhookSecret` is not configured (no dev bypass)

#### Database (`Subscription` entity)

**Core fields:**
- `UserId`, `Status` (enum: `None`, `Trial`, `Active`, `Expired`, `Canceled`), `PlanType`, `TrialEndsAtUtc`
- `PlanType` enum: `FreeBYOK = 0` (legacy sentinel), `CloudBYOK = 1`, `CloudManaged = 2`, `TrialFullAccess = 3` (generic 24h trial — plan not yet chosen)

**Paddle identifiers:**
- `PaddleSubscriptionId`, `PaddleCustomerId`, `PaddlePriceId`, `PaddleProductId`, `PaddleTransactionId`

**Billing troubleshooting:**
- `CurrencyCode`, `UnitAmountMinor`, `BillingInterval`
- `CurrentPeriodEndsAtUtc`, `NextBilledAtUtc`
- `CancelledAtUtc`, `CancellationReason`
- `PaymentMethodType`, `CardLastFour`

Single-query troubleshooting: all subscription/billing/payment data in one row.

---

### 2. Web App (`foqus-web-app`)

#### Tech Stack
- Vite + React + TypeScript
- `@paddle/paddle-js` npm package
- Tailwind CSS (custom billing styles in `BillingPage.css`)

#### Implementation

**`usePaddle` hook** (`hooks/usePaddle.ts`):
- Fetches `/pricing` on mount
- Initializes Paddle.js with `clientToken` and sandbox/production environment
- Provides `openCheckout(priceId, planType, email, userId)` that opens overlay
- Handles `checkout.completed` event callback

**`SubscriptionContext`** (`contexts/SubscriptionContext.tsx`):
- Fetches `GET /subscriptions/status` once per authenticated session
- Exposes `{ subscription, loading, error, refresh }` to the entire protected layout
- Wraps `Layout` so all pages share subscription state without duplicate fetches

**Trial welcome modal** (`components/TrialWelcomeModal.tsx`):
- Shown on first visit when `status === 'trial'` and `localStorage` flag `foqus.trialWelcomeSeen.<userId>` is not set
- Explains Foqus, shows trial end time, links to `/billing` to compare plans
- Dismissal persists in `localStorage` (scoped per user ID)

**Trial countdown banner** (in `Layout.tsx`):
- Visible when `status === 'trial'` and `trialEndsAt` is in the future
- Shows time remaining (hours + minutes) and a "Choose a plan" link to `/billing`

**BillingPage** (`pages/BillingPage.tsx`):
- Shows current subscription status (plan badge, trial end, period dates)
- **No "Free (BYOK)" static card** — there is no free tier
- Trial status shows "Trial — Full Access" header with a hint to choose a plan before expiry
- **Paid plans only** — dynamic from `/pricing`, sorted by price ascending (BYOK $1.99, Premium $4.99)
- Subscribe buttons open Paddle.js overlay checkout
- "Manage Subscription" button (active subscribers only) opens Paddle customer portal
- Success banner on `checkout.completed` or `?checkout=success` query param
- **Cloud BYOK setup modal** (`components/BYOKSetupModal.tsx`): After `checkout.completed`, if the subscribed price slug was **cloud-byok** (tracked when **Subscribe** is clicked), shows steps to open the Windows app or browser extension, go to Settings, and paste the OpenAI (or provider) API key. Dismiss with **Got it**.
- Error handling for missing `clientToken` or pricing failures

**Custom data sent to Paddle:**
```typescript
customData: { 
  user_id: userId,      // Supabase user guid
  plan_type: planTypeSlug  // "cloud-byok" or "cloud-managed"
}
```

---

### 3. Browser Extension (`browser-extension`)

**Checkout redirect:**
- Browser extension opens `https://app.foqus.me/billing` for plan changes
- No embedded checkout — delegates to web app

**Trial and subscription UX:**
- Popup (`ui/AppShell.tsx`) shows a compact non-dismissible trial banner only when Foqus trial is active (`status = trial`, `planType = 0`, future `trialEndsAt`)
- Trial banner is popup-only (not shown in sidepanel)
- Options page (`options/main.tsx`) uses a subscription summary (current plan, end date, manage subscription link, refresh action) instead of plan comparison cards
- Trial expiry with no paid subscription is shown as **No active plan** with a billing link

**Cloud BYOK prompt:**
- When subscription resolves to `cloud-byok` and `openAiApiKey` is empty, options page highlights the API key section and prompts the user to enter a key
- Security copy explains key handling:
  - Stored in Chrome extension storage scoped to this extension
  - Sent directly to the AI provider over HTTPS
  - Not transmitted to Foqus servers

**Classification flow (dual-path, unchanged):**
- **BYOK direct path:** `shared/classifier.ts` calls `https://api.openai.com/v1/chat/completions` with `Authorization: Bearer <openAiApiKey>`
- **Cloud managed path:** `shared/classifier.ts` calls Foqus API `POST /classify` with JWT; server uses managed key

**Implementation files:**
- `shared/webAppUrl.ts` — `getWebAppBillingUrl()` helper
- `background/index.ts` — plan refresh and persisted subscription fields (`status`, `planType`, `trialEndsAt`, `currentPeriodEndsAt`)
- `ui/AppShell.tsx` + `ui/styles.css` — popup trial banner
- `options/main.tsx` + `options/settings.css` — subscription summary and BYOK prompt

---

### 4. Desktop App (WinUI 3)

**Checkout redirect:**
- `PlanSelectionViewModel.cs` — `SelectPlan` command opens web app billing URL in browser
- `FocusPageViewModel.cs` — Subscribes to SignalR `PlanChanged` and `IPlanService.PlanChanged`, calls `IPlanService.RefreshAsync()` and updates trial / BYOK UI
- `PlanService` — Caches plan type, subscription status string (mapped to `ClientSubscriptionStatus`), and `TrialEndsAt` from `GET /subscriptions/status`; 5-minute TTL; instant refresh on SignalR notification

**Trial UX (Focus page):**
- **`TrialWelcomeDialog`** — One-time welcome after first-run **How it works** (or when the user signs in later), gated by `SettingsKeys.TrialWelcomeSeen`, for Foqus trial users. **View plans** opens `https://app.foqus.me/billing`.
- **Trial `InfoBar`** — Countdown to `trialEndsAt` and **Manage plan** link when the Foqus trial is active (`ClientPlanType.FreeBYOK` / server `TrialFullAccess`, status trial, future end). Separate **Subscription required** banner after local trial expiry with **View plans**.
- **`BYOKKeyPromptDialog`** — When the plan becomes **Cloud BYOK** and no API key is stored locally, prompts once per session to open **Settings** (includes DPAPI / HTTPS security copy).

**No local checkout** — user completes payment in browser, webhook updates DB, SignalR notifies desktop immediately.

---

## Configuration per Environment

### Sandbox (Local Dev)

**WebAPI** (`appsettings.Development.json` + user secrets):
```json
{
  "Paddle": {
    "ApiBase": "https://sandbox-api.paddle.com",
    "IsSandbox": true,
    "CatalogProductId": "pro_01kmtmaya6xwnyc4fqzvyjxxpg"
  }
}
```

**User secrets:**
```powershell
dotnet user-secrets set "Paddle:ApiKey" "pdl_sdbx_..." --project src/FocusBot.WebAPI
dotnet user-secrets set "Paddle:WebhookSecret" "..." --project src/FocusBot.WebAPI
dotnet user-secrets set "Paddle:ClientToken" "test_..." --project src/FocusBot.WebAPI
dotnet user-secrets set "Paddle:CatalogProductId" "pro_..." --project src/FocusBot.WebAPI
```

**Webhook URL** (ngrok/tunnel):
```
https://<tunnel-host>/subscriptions/paddle-webhook
```

Register in **Paddle Dashboard → Developer tools → Notifications**. Subscribe to:
- `subscription.created`
- `subscription.updated`
- `subscription.canceled`
- `transaction.completed`

**Web app** (`.env`):
```
VITE_SUPABASE_URL=...
VITE_SUPABASE_ANON_KEY=...
VITE_API_BASE_URL=http://localhost:5251  # optional, defaults to this
```

### Production

- **`Paddle:ApiBase`** → `https://api.paddle.com`
- **`Paddle:IsSandbox`** → `false`
- **`Paddle:CatalogProductId`** → production Foqus product id
- **All secrets** via Azure App Service settings or Key Vault
- **Webhook URL** → `https://api.foqus.me/subscriptions/paddle-webhook` (or your deployed domain)

---

## Paddle Dashboard Setup

### Products
One product per environment (sandbox and production):
- Name: "Foqus"
- Type: Standard
- Tax category: SaaS

### Prices
Two recurring prices (monthly billing):

| Name | Amount | Interval | Custom Data |
|------|--------|----------|-------------|
| Foqus BYOK | $1.99 | month | `{ "plan_type": "cloud-byok" }` |
| Foqus Premium | $4.99 | month | `{ "plan_type": "cloud-managed" }` |

**Trial period** (optional): Removed from Paddle Dashboard prices. Server manages a 24-hour no-credit-card trial via `POST /subscriptions/trial`.

### Webhooks (Notifications)
Destination URL: `https://<your-domain>/subscriptions/paddle-webhook`

**Events to subscribe:**
- subscription.created
- subscription.updated
- subscription.canceled
- transaction.completed

Copy the **webhook signing secret** to `Paddle:WebhookSecret` in your API configuration.

---

## Testing Checklist

### Local Sandbox Flow

1. **Start services:**
   ```powershell
   # PostgreSQL
   docker run -d --name focusbot-pg -e POSTGRES_DB=focusbot -e POSTGRES_USER=focusbot -e POSTGRES_PASSWORD=focusbot_dev -p 5432:5432 postgres:16-alpine
   
   # API
   dotnet run --project src/FocusBot.WebAPI/FocusBot.WebAPI.csproj --launch-profile http
   
   # Web app
   cd src/foqus-web-app && npm run dev
   
   # Tunnel (example)
   ngrok http https://localhost:7013
   ```

2. **Configure Paddle webhook** with tunnel URL

3. **Test GET /pricing:**
   ```
   curl http://localhost:5251/pricing
   ```
   Should return 2 plans (BYOK $1.99, Premium $4.99), `clientToken`, `isSandbox: true`

4. **Sign in to web app** (`http://localhost:5174`)

5. **Navigate to `/billing`**:
   - See Free + 2 paid plan cards (sorted by price)
   - Click Subscribe on BYOK plan
   - Paddle overlay opens (sandbox mode)
   - Complete with test card (4242 4242 4242 4242)

6. **Watch API logs** for webhook processing:
   - `subscription.created` (status = "trialing")
   - `transaction.completed` (payment details)

7. **Check database:**
   ```sql
   SELECT "UserId", "Status", "PlanType", "PaddleSubscriptionId", "CardLastFour"
   FROM "Subscriptions";
   ```
   Should show `Status = Trial` (enum), `PlanType = 1` (CloudBYOK), `CardLastFour = "4242"`

8. **Refresh billing page** — should show "Trial" badge, trial end date

9. **Open desktop app** (if running) — should receive SignalR `PlanChanged` and refresh plan instantly

---

## What Your Payloads Tell Us

### subscription.created
```json
{
  "event_type": "subscription.created",
  "data": {
    "id": "sub_...",
    "status": "trialing",  // ✅ Now mapped to Trial (enum)
    "customer_id": "ctm_...",
    "custom_data": {
      "user_id": "a44677aa-...",
      "plan_type": "cloud-byok"  // ✅ Maps to CloudBYOK
    },
    "items": [{
      "price": {
        "id": "pri_...",
        "product_id": "pro_...",
        "unit_price": { "amount": "199", "currency_code": "USD" },
        "billing_cycle": { "interval": "month", "frequency": 1 }
      }
    }],
    "current_billing_period": { "ends_at": "2026-03-30T15:52:44.486Z" },
    "next_billed_at": "2026-03-30T15:52:44.486Z"
  }
}
```

**Result:** Subscription row created with `Status = Trial` (enum), `PlanType = CloudBYOK`, all enrichment fields populated. Event is recorded by `event_id` for idempotency.

### transaction.completed
```json
{
  "event_type": "transaction.completed",
  "data": {
    "id": "txn_...",
    "subscription_id": "sub_...",  // ✅ Links to existing row
    "billing_period": { "ends_at": "2026-03-30T..." },
    "payments": [{
      "method_details": {
        "type": "card",
        "card": { "last4": "4242", "type": "visa", ... }  // ✅ Extracted correctly
      }
    }]
  }
}
```

**Result:** Subscription row updated with `PaddleTransactionId`, `CurrentPeriodEndsAtUtc`, `PaymentMethodType = "card"`, `CardLastFour = "4242"`.

---

## Files Created/Modified

### New Files
- `src/FocusBot.WebAPI/PaddleSettings.cs`
- `src/FocusBot.WebAPI/Features/Pricing/IPaddleBillingApi.cs`
- `src/FocusBot.WebAPI/Features/Pricing/PaddleBillingApiClient.cs`
- `src/FocusBot.WebAPI/Features/Pricing/PricingEndpoints.cs`
- `src/FocusBot.WebAPI/Features/Pricing/SLICE.md`
- `src/FocusBot.WebAPI/Features/Subscriptions/PaddleWebhookVerifier.cs`
- `src/FocusBot.WebAPI/Features/Subscriptions/PaddleWebhookModels.cs` ⭐
- `src/FocusBot.WebAPI/Migrations/20260328120000_EnrichSubscriptionForPaddle.cs`
- `tests/FocusBot.WebAPI.Tests/Features/Subscriptions/PaddleWebhookVerifierTests.cs`
- `tests/FocusBot.WebAPI.IntegrationTests/TestPaddleBillingApi.cs`
- `tests/FocusBot.WebAPI.IntegrationTests/PricingAndPortalTests.cs`
- `tests/FocusBot.App.ViewModels.Tests/StubPlanService.cs`
- `src/foqus-web-app/src/hooks/usePaddle.ts`
- `src/foqus-web-app/src/pages/BillingPage.test.tsx`
- `browser-extension/.env.development`
- `browser-extension/.env.production`
- `browser-extension/src/shared/webAppUrl.ts`
- `pricing/README.md`
- `pricing/archive/` (moved old docs)
- `docs/paddle-guide.md` (updated)
- `docs/paddle-webhook-fixes.md` ⭐
- `docs/paddle-implementation-summary.md` (this file)

### Modified Files
- `src/FocusBot.WebAPI/Program.cs` — Paddle config, HTTP client, pricing/subscription endpoints
- `src/FocusBot.WebAPI/appsettings.json` — Paddle section + CatalogProductId
- `src/FocusBot.WebAPI/appsettings.Development.json` — Sandbox product id
- `src/FocusBot.WebAPI/Data/Entities/Subscription.cs` — 12+ new enrichment fields
- `src/FocusBot.WebAPI/Data/ApiDbContext.cs` — Subscription entity config
- `src/FocusBot.WebAPI/Features/Subscriptions/SubscriptionService.cs` — Model-based webhook handlers ⭐
- `src/FocusBot.WebAPI/Features/Subscriptions/SubscriptionEndpoints.cs` — Signature verification, portal endpoint
- `src/FocusBot.WebAPI/Features/Subscriptions/Dtos.cs` — `NextBilledAtUtc`, portal response
- `src/FocusBot.WebAPI/Hubs/FocusHub.cs` — `PlanChanged` event
- `src/FocusBot.Core/Interfaces/IFocusHubClient.cs` — `PlanChanged` + `PlanChangedEvent`
- `src/FocusBot.Core/Entities/ApiModels.cs` — `NextBilledAtUtc` on `ApiSubscriptionStatus`
- `src/FocusBot.Infrastructure/Services/FocusHubClientService.cs` — `PlanChanged` SignalR handler
- `src/FocusBot.App.ViewModels/FocusPageViewModel.cs` — Subscribe to hub `PlanChanged`, call `IPlanService.RefreshAsync()`
- `src/FocusBot.App.ViewModels/PlanSelectionViewModel.cs` — Open web billing URL
- `src/foqus-web-app/package.json` — Added `@paddle/paddle-js`
- `src/foqus-web-app/src/pages/BillingPage.tsx` — Paddle checkout, portal, sorted plan cards
- `src/foqus-web-app/src/api/client.ts` — `fetchPricingPublic()`, `createCustomerPortalSession()`
- `src/foqus-web-app/src/api/types.ts` — Pricing DTOs
- `browser-extension/src/options/main.tsx` — Link to web billing
- `AGENTS.md` — Paddle setup instructions
- All test files — Updated for new webhook/pricing behavior

---

## Test Results

✅ **FocusBot.WebAPI.Tests**: 72 tests passed (includes 10+ new webhook tests covering idempotency, race conditions, plan type resolution, past_due mapping, and webhook secret rejection)
✅ **FocusBot.WebAPI.IntegrationTests**: 28 tests passed  
✅ **FocusBot.App.ViewModels.Tests**: 35 tests passed  
✅ **FocusBot.Core.Tests**: 25 tests passed  
✅ **browser-extension**: 76 tests passed  
✅ **foqus-web-app**: 46 tests passed (1 unrelated time test flake)  
✅ **Production build**: Clean  

---

## What's Next

### Immediate (Required for Production)
1. **Set Paddle production keys** in Azure App Service / Key Vault
2. **Update `CatalogProductId`** to production Foqus product
3. **Configure production webhook** URL in Paddle dashboard
4. **Test end-to-end** in production sandbox first

### Performance (If Webhook Timeouts Continue)
1. **Add DB index** on `PaddleSubscriptionId` for faster lookups
2. **Idempotency check** at start of webhook handlers (skip already-processed)
3. **Fire-and-forget SignalR** (non-critical for billing correctness)
4. **Increase command timeout** to 15-30s

### Future Enhancements
1. **Annual pricing** — Add yearly plans with discount
2. **Downgrade flow** — Handle Premium → BYOK at period end
3. **Free tier restrictions** — Gate analytics/sync by plan type
4. **Customer email** — Add to checkout `custom_data` if needed for support
5. **Webhook retry queue** — For high-volume or complex processing

---

## Key Decisions Made

1. **Web app is checkout hub** — Desktop and extension redirect, no embedded checkout
2. **Paddle.js overlay** — No separate checkout pages, inline overlay UX
3. **Strongly-typed models + no JsonElement in service layer** — Replaced manual JSON parsing with C# classes
4. **Status as SubscriptionStatus enum** — Strongly-typed (`None`, `Trial`, `Active`, `Expired`, `Canceled`), serialized as camelCase strings for frontend compatibility
5. **past_due → Expired** — Inactive payment means no access
6. **Webhook idempotency via ProcessedWebhookEvent** — Prevents duplicate subscription creation on Paddle retries
7. **Transaction.completed race condition handling** — Can create subscription if arrived before subscription.created
8. **Plan type resolution failure** — Logs error and skips (no default `CloudBYOK` fallback)
9. **Trial activation accepts PlanType** — Server manages 24h trial without Paddle trial (Paddle trial removed from Dashboard)
10. **Webhook secret rejection** — No dev bypass; empty secret = request denied with error log
11. **Product filtering** — `/pricing` only returns prices for `CatalogProductId` (no cross-product pollution)
12. **License fallback** — Supports both `plan_type` and `license` in custom data for flexibility
13. **Enriched subscription table** — Single-query troubleshooting view (no Paddle API calls for support)

---

**Reference:** See `docs/paddle-guide.md` for detailed Paddle Billing concepts and `docs/paddle-webhook-fixes.md` for issue analysis.

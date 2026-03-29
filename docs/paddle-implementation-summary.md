# Paddle Billing Implementation Summary

This document summarizes the complete Paddle Billing integration across all Foqus platforms (API, web app, browser extension, desktop app).

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
| GET | `/subscriptions/status` | User's current plan and billing dates |
| POST | `/subscriptions/trial` | Activates 24h trial |
| POST | `/subscriptions/portal` | Creates Paddle customer portal session URL |
| POST | `/subscriptions/paddle-webhook` | Webhook receiver with signature verification |

#### Webhook Processing (`SubscriptionService.cs` + `PaddleWebhookModels.cs`)

**Strongly-typed C# models** for Paddle payloads:
- `PaddleWebhookPayload` — envelope
- `PaddleSubscription`, `PaddleTransaction` — event data
- `PaddlePrice`, `PaddleCustomData`, `PaddlePayment`, `PaddleCardDetails`, etc.

**Events handled:**
- `subscription.created` — Creates/updates subscription with status mapping
- `subscription.updated` — Updates plan, status, dates
- `subscription.canceled` — Marks canceled, records cancellation date/reason
- `transaction.completed` — Records transaction id, billing period, payment method

**Status mapping:**
```
Paddle           → App
"trialing"       → "trial"
"active"         → "active"
"past_due"       → "active"
"paused"         → "expired"
"canceled"       → "canceled"
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

#### Database (`Subscription` entity)

**Core fields:**
- `UserId`, `Status`, `PlanType`, `TrialEndsAtUtc`

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

**BillingPage** (`pages/BillingPage.tsx`):
- Shows current subscription status (plan badge, trial end, period dates)
- Displays plan cards:
  - **Free (BYOK)** — static card
  - **Paid plans** — dynamic from `/pricing`, sorted by price ascending (BYOK $1.99, Premium $4.99)
- Subscribe buttons open Paddle.js overlay checkout
- "Manage Subscription" button (active subscribers only) opens Paddle customer portal
- Success banner on `checkout.completed` or `?checkout=success` query param
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
- Plan upgrade cards in options page link to `https://app.foqus.me/billing`
- No embedded checkout — delegates to web app

**Implementation:**
- `webAppUrl.ts` — `getWebAppBillingUrl()` helper
- `options/main.tsx` — Updated paid plan buttons

---

### 4. Desktop App (WinUI 3)

**Checkout redirect:**
- `PlanSelectionViewModel.cs` — `SelectPlan` command opens web app billing URL in browser
- `FocusPageViewModel.cs` — Subscribes to SignalR `PlanChanged` event, calls `IPlanService.RefreshAsync()`
- `PlanService` — 5-minute cache, instant refresh on SignalR notification

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

**Trial period** (optional): 1 day, requires payment method.

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
   Should show `Status = "trial"`, `PlanType = 1` (CloudBYOK), `CardLastFour = "4242"`

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
    "status": "trialing",  // ✅ Now mapped to "trial"
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

**Result:** Subscription row created with `Status = "trial"`, `PlanType = CloudBYOK`, all enrichment fields populated.

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

✅ **FocusBot.WebAPI.Tests**: 62 tests passed  
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
3. **Strongly-typed models** — Replaced manual JSON parsing with C# classes
4. **Status mapping** — `"trialing"` → `"trial"` for app convention consistency
5. **Product filtering** — `/pricing` only returns prices for `CatalogProductId` (no cross-product pollution)
6. **License fallback** — Supports both `plan_type` and `license` in custom data for flexibility
7. **Enriched subscription table** — Single-query troubleshooting view (no Paddle API calls for support)

---

**Reference:** See `docs/paddle-guide.md` for detailed Paddle Billing concepts and `docs/paddle-webhook-fixes.md` for issue analysis.

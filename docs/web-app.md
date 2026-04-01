# Foqus Web App

Cloud dashboard at `app.foqus.me` — React 19 SPA for session management, analytics, billing, and cross-device sync. Deployed as an Azure Static Web App.

---

## Architecture

```
main.tsx
  └── <BrowserRouter>
        └── <AuthProvider>              ← Supabase session context
              └── <Routes>
                    ├── /login           (public)
                    ├── /auth/callback   (public)
                    ├── /terms|/privacy|/refund  (public)
                    └── <ProtectedRoute>
                          └── <Layout>   ← sidebar + nav
                                └── <SubscriptionProvider>
                                      └── <LayoutContent>
                                            ├── TrialBanner
                                            ├── TrialWelcomeModal
                                            └── <Outlet>  (page)
```

### Provider Hierarchy

1. **AuthProvider** — outermost, wraps entire tree
2. **ProtectedRoute** — redirects to `/login` if no session
3. **SubscriptionProvider** — inside Layout only (protected routes)

---

## Routes

| Path | Component | Auth | Layout |
|---|---|---|---|
| `/login` | `LoginPage` | Public | None |
| `/auth/callback` | `AuthCallbackPage` | Public | None |
| `/terms` | `TermsPage` | Public | None |
| `/privacy` | `PrivacyPage` | Public | None |
| `/refund` | `RefundPage` | Public | None |
| `/` | `DashboardPage` | Protected | Layout |
| `/analytics` | `AnalyticsPage` | Protected | Layout |
| `/integrations` | `IntegrationsPage` | Protected | Layout |
| `/settings` | `SettingsPage` | Protected | Layout |
| `/billing` | `BillingPage` | Protected | Layout |
| `*` | `Navigate to="/"` | — | — |

---

## Authentication

### Supabase Client (`auth/supabase.ts`)

- `createClient(url, anonKey)` from `@supabase/supabase-js`
- Production defaults hardcoded; dev overrides via `VITE_SUPABASE_URL` and `VITE_SUPABASE_ANON_KEY`

### AuthProvider (`auth/AuthProvider.tsx`)

- On mount: `supabase.auth.getSession()` → sets `session`, `user`, `loading=false`
- Subscribes to `supabase.auth.onAuthStateChange` for live updates
- `signOut()` calls `supabase.auth.signOut()` then clears state

### Login Flow

1. `LoginPage`: user enters email → `supabase.auth.signInWithOtp({ email, emailRedirectTo: origin + "/auth/callback" })`
2. User clicks magic link in email
3. `AuthCallbackPage`: calls `supabase.auth.getSession()` → on success navigates to `/`

### Token Management (API Client)

- Every API call gets fresh `session.access_token` from `supabase.auth.getSession()`
- On 401: calls `supabase.auth.refreshSession()` → re-reads session → retries request
- Single retry on token refresh

---

## API Client (`api/client.ts`)

Two internal helpers:
- `apiFetch<T>(path, init)` — GET requests, returns `T | null`, auto-injects JWT, 401 refresh+retry
- `apiMutate<T>(path, init)` — mutations, returns `ApiMutationResult<T>` (`{ ok, data }` or `{ ok: false, status, error }`), same retry logic

### API Methods

| Method | HTTP | Endpoint | Purpose |
|---|---|---|---|
| `getMe()` | GET | `/auth/me` | Provision user + start trial |
| `deleteAccount()` | DELETE | `/auth/account` | Account deletion |
| `getSubscriptionStatus()` | GET | `/subscriptions/status` | Plan, trial, billing dates |
| `createCustomerPortalSession()` | POST | `/subscriptions/portal` | Paddle customer portal URL |
| `activateTrial(planType)` | POST | `/subscriptions/trial` | Explicit trial activation |
| `getActiveSession()` | GET | `/sessions/active` | Current in-progress session |
| `getSessions(params)` | GET | `/sessions` | Paginated, filterable |
| `getSession(id)` | GET | `/sessions/{id}` | Single session |
| `startSession(req)` | POST | `/sessions` | Start focus session |
| `endSession(id, req)` | POST | `/sessions/{id}/end` | End with summary |
| `pauseSession(id)` | POST | `/sessions/{id}/pause` | Pause session |
| `resumeSession(id)` | POST | `/sessions/{id}/resume` | Resume session |
| `getAnalyticsSummary(params)` | GET | `/analytics/summary` | Summary stats |
| `getAnalyticsTrends(params)` | GET | `/analytics/trends` | Trend data points |
| `getAnalyticsClients(params)` | GET | `/analytics/clients` | Per-client breakdown |
| `fetchPricingPublic()` | GET | `/pricing` | Unauthenticated pricing data |

---

## SignalR Integration (`api/signalr.ts`)

- Connects to `{API_BASE_URL}/hubs/focus` using `@microsoft/signalr`
- **Auth**: `accessTokenFactory` gets JWT from `supabase.auth.getSession()`
- **Reconnection**: `withAutomaticReconnect([0, 2000, 5000, 10000, 30000])`
- **Singleton**: Module-level connection, reuses if already connected
- Custom logger swallows "stopped during negotiation" noise from React StrictMode

### Events Consumed

| Event | Handler (DashboardPage) |
|---|---|
| `SessionStarted` | Optimistic state update → refresh active + today data |
| `SessionEnded` | Refresh active + today data |
| `SessionPaused` | Refresh active + today data |
| `SessionResumed` | Refresh active + today data |
| `ClassificationChanged` | Update `liveClassification` for real-time display |

DashboardPage subscribes in `useEffect` gated on auth session; disconnects on unmount.

---

## Subscription & Billing

### SubscriptionContext (`contexts/SubscriptionContext.tsx`)

- Provides `subscription`, `loading`, `error`, `refresh()`
- On mount (when `userId` changes): calls `loadSubscriptionForUser(userId)`

### Subscription Bootstrap (`contexts/subscriptionBootstrap.ts`)

- `loadSubscriptionForUser(userId)` — single-flight per userId via `Map<string, Promise>`
- Calls `api.getMe()` (provisions user + creates 24h trial) → `api.getSubscriptionStatus()`
- Deduplicates concurrent calls from StrictMode / multiple renders

### usePaddle Hook (`hooks/usePaddle.ts`)

- Returns `{ pricing, loadError, ready, openCheckout }`
- On mount: fetches `fetchPricingPublic()` (unauthenticated `GET /pricing`)
- If pricing + `clientToken`: initializes Paddle.js (`sandbox` or `production` environment)
- Listens for `checkout.completed` event → fires `onCheckoutCompleted` callback
- `openCheckout(priceId, planTypeSlug, email, userId)` opens Paddle overlay with `customData: { user_id, plan_type }`

### BillingPage

1. Shows current plan info: name, status badge, trial/period end dates
2. Lists paid plans from API (sorted by price ascending)
3. Each `PlanCard`: name, price, features, subscribe button → `openCheckout`
4. "Manage subscription" button (when `status === "active"`) → opens Paddle customer portal
5. After checkout completion:
   - Success banner
   - Triggers `reloadSubscription()`
   - If plan was `cloud-byok` → shows `BYOKSetupModal`
6. Handles `?checkout=success` query parameter for redirect flows

### BYOKSetupModal

Post-checkout for Cloud BYOK plan:
- Steps: Open Windows app or extension → Settings → Paste API key
- Notes: API key encrypted locally, not stored on servers
- Dismissable via button or backdrop click

### TrialWelcomeModal

- Shown once per user on Foqus trial (`planType === TrialFullAccess && status === "trial"`)
- Content: 24h full access, no credit card, trial end datetime, plan comparison link
- Dismiss tracked in `localStorage` via `foqus.trialWelcomeSeen.{userId}`
- Closes on button, backdrop, or Escape

### TrialBanner (in Layout)

- Fixed banner when user is on trial with time remaining
- Shows countdown: `{hours}h {minutes}m remaining`
- "Choose a plan" links to `/billing`

---

## Pages

### DashboardPage (479 lines)

**Data loading**:
- `loadTodayData()` — fetches analytics summary + sessions for today
- `refreshActive()` — fetches active session; polls every 5s while active
- SignalR hub connected for real-time updates

**Active session panel**:
- Session title, start time, paused status
- `SessionTimer` for live elapsed time
- Live classification: Aligned/Distracting/Neutral with activity name + reason
- Pause/Resume/End controls

**Start session form** (no active session):
- Task title (required) + optional context
- "Start focus session" button

**Today's KPIs** (4 `KpiCard` components):
- Focus score (donut gauge + time breakdown)
- Deep work (total focused seconds)
- Sessions (count + total time)
- Distractions (count + avg duration)

**Session history** (collapsible table):
- Completed sessions: Task, Started, Focus%, Duration
- Color-coded focus percentages

### AnalyticsPage

- Date range presets via `SegmentedControl`: Today, 7d, 30d
- KPI cards: Deep Work, Focus %, Distractions, Sessions
- Trend chart: `recharts` area chart (daily focus minutes + distraction minutes)
- Session history table: Task, Date, Focus%, Duration with color-coded scores
- Per-client breakdown (if multiple devices)

### IntegrationsPage

- Desktop app integration info
- Browser extension integration info
- Links to downloads / setup instructions

### SettingsPage

- Account info (email)
- Delete account action

---

## Components

| Component | Purpose |
|---|---|
| `Layout` | App shell: sidebar nav, user info, sign out, legal links, mobile hamburger. Wraps children in `SubscriptionProvider`. |
| `FocusGauge` | SVG ring progress (0-100%). Uses `focusGaugeMath.ts` for consistent stroke math (shared with extension). |
| `KpiCard` | Metric card (label/value/sublabel). Variants: `default`, `aligned`, `distracted`, `focus-score` (includes `FocusGauge`). |
| `SessionTimer` | Live elapsed timer. Updates 1s. Computes active seconds excluding paused time. Freezes when paused. Accepts `nowMs` prop for deterministic tests. |
| `SegmentedControl` | Generic segmented button group (`<T extends string>`). Uses `aria-pressed`. |
| `BYOKSetupModal` | Post-checkout Cloud BYOK setup instructions. |
| `TrialWelcomeModal` | One-time trial welcome. Per-user localStorage tracking. |
| `PlanCard` | Plan comparison card (inline in BillingPage). |

---

## Hooks

| Hook | Source | Purpose |
|---|---|---|
| `useAuth` | `auth/useAuth.ts` | Access auth context (session, user, loading, signOut) |
| `useSubscription` | `contexts/SubscriptionContext.tsx` | Access subscription context (status, loading, error, refresh) |
| `usePaddle` | `hooks/usePaddle.ts` | Initialize Paddle.js, fetch pricing, expose `openCheckout` |

---

## Utilities

| Module | Purpose |
|---|---|
| `utils/analyticsDisplay.ts` | Focus score tone/class, active seconds computation, chart helpers, averages |
| `utils/focusGaugeMath.ts` | SVG gauge stroke calculations (shared logic with extension) |
| `utils/format.ts` | Duration formatting, date helpers (`daysAgo`, `startOfLocalDayIso`, `formatDate/DateTime`) |

---

## Types (`api/types.ts`)

### Enums

```typescript
enum PlanType { TrialFullAccess = 0, FreeBYOK = 1, CloudBYOK = 2, CloudManaged = 3 }
enum SubscriptionStatus { None = "none", Trial = "trial", Active = "active", Expired = "expired", Canceled = "canceled" }
```

### Key Interfaces

- `SubscriptionStatusResponse` — planType, status, trialEndsAt, currentPeriodEndsAt, nextBilledAtUtc, paddleCustomerId
- `ActiveSessionResponse` — sessionId, sessionTitle, sessionContext, startedAtUtc, pausedAtUtc, isPaused, clientId
- `SessionResponse` — full session with endedAtUtc + summary fields
- `AnalyticsSummaryResponse` — totalSessions, totalTrackedSeconds, totalAligned/DistractedSeconds, totalDistractions, focusScorePercent, clientsActive
- `AnalyticsTrendDataPoint` — date, totalTrackedSeconds, aligned/distractedSeconds, focusScorePercent, sessionCount
- `PricingResponse` — plans[], clientToken, isSandbox
- `PricingPlan` — id, name, slug, planType, monthlyPrice, currency, billingInterval, features[]

---

## Legal Pages

Self-contained Terms, Privacy, and Refund pages. Content duplicated from website (same sections, entity, dates). Back link goes to Dashboard instead of landing page.

- **Entity**: SC NeuroQode Solutions SRL, Romania
- **Last updated**: March 21, 2026
- **Key policies**: 30-day refund, GDPR-compliant, data deletion within 30 days

---

## Environment Configuration

| Variable | Purpose | Default |
|---|---|---|
| `VITE_SUPABASE_URL` | Supabase project URL | Hardcoded production fallback |
| `VITE_SUPABASE_ANON_KEY` | Supabase publishable key | Hardcoded production fallback |
| `VITE_API_BASE_URL` | WebAPI base URL | `http://localhost:5251` (dev), `https://api.foqus.me` (prod) |

### Vite Config

- Plugin: `@vitejs/plugin-react`
- Dev server: port `5174`, `fs.allow: [".."]`
- Build output: `dist/`

### Vitest Config

- Environment: `jsdom`
- Globals: `false`
- Setup: `./src/test/setup.ts` (jest-dom matchers, Supabase mock, cleanup)
- Test env vars set for Supabase (avoids real client init)

---

## Deployment (Azure Static Web App)

**Config**: `public/staticwebapp.config.json`

```json
{
  "navigationFallback": {
    "rewrite": "/index.html",
    "exclude": ["/assets/*", "/*.svg", "/*.ico", "/*.png", "/*.jpg"]
  },
  "routes": [
    { "route": "/api/*", "allowedRoles": ["anonymous"] }
  ],
  "responseOverrides": {
    "401": { "redirect": "/login", "statusCode": 302 }
  },
  "globalHeaders": {
    "X-Content-Type-Options": "nosniff",
    "X-Frame-Options": "DENY",
    "Referrer-Policy": "strict-origin-when-cross-origin"
  }
}
```

- SPA fallback to `index.html` for client-side routing
- 401 responses redirect to `/login`
- Security headers: nosniff, DENY framing, strict-origin referrer

---

## Testing

**Stack**: Vitest 4 + jsdom + @testing-library/react + @testing-library/user-event + @testing-library/jest-dom

**Test setup** (`src/test/setup.ts`): mocks Supabase client globally, cleanup after each test.

| Test File | Tests | Coverage |
|---|---|---|
| `api/client.test.ts` | 5 | startSession/endSession body, pause/resume, 409 handling |
| `components/FocusGauge.test.tsx` | 2 | SVG stroke math, attribute rendering |
| `components/KpiCard.test.tsx` | 3 | Label/value, aligned variant, focus-score gauge |
| `components/SessionTimer.test.tsx` | 2 | Active seconds, freeze on pause |
| `components/SegmentedControl.test.tsx` | 1 | Active highlight + onChange |
| `components/BYOKSetupModal.test.tsx` | 3 | Open/closed rendering, dismiss |
| `components/TrialWelcomeModal.test.tsx` | 6 | Content, button/backdrop/escape dismiss, localStorage |
| `utils/analyticsDisplay.test.ts` | ~14 | Focus score tone, chart helpers, live session seconds |
| `utils/format.test.ts` | 5 | Duration formatting, date helpers |
| `pages/DashboardPage.test.tsx` | 7 | Start form, active panel, KPIs, pause/end session |
| `pages/BillingPage.test.tsx` | 6 | Plan loading, trial header, checkout, portal |
| `pages/AnalyticsPage.test.tsx` | 4 | KPI display, empty state, trend chart, focus score color |

**Run**: `cd src/foqus-web-app && npm test`

---

## Key Dependencies

| Package | Version | Purpose |
|---|---|---|
| `react` | ^19.2.4 | UI framework |
| `react-dom` | ^19.2.4 | DOM rendering |
| `react-router-dom` | ^7.6.1 | Client-side routing |
| `@supabase/supabase-js` | ^2.49.8 | Authentication |
| `@microsoft/signalr` | ^10.0.0 | Real-time sync |
| `@paddle/paddle-js` | ^1.6.2 | Checkout overlay |
| `recharts` | ^3.8.0 | Analytics charts |

---

## Building

```bash
cd src/foqus-web-app

# Install
npm install

# Dev (port 5174)
npm run dev

# Build
npm run build

# Tests
npm test
npm run test:watch
```

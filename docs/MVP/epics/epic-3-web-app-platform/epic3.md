# Epic 3 — Web App Platform Shell & Auth

## Objective

Stand up `app.foqus.me` as a separate SPA deployment with authentication, account management, integrations page, and device management. This is the cloud hub — the single place where paid users view integrations, access full analytics (Epic 5), and manage their account.

**Depends on:** Epic 1 (Shared Contracts & Entitlement Model)

> This epic merges the original Phases 3 (Web App Infrastructure) and 4 (Web App Login & Integrations) because they are tightly coupled and should ship together.

---

## Current State

| Component | Status | Details |
|---|---|---|
| Marketing site (`foqus.me`) | `[EXISTS]` | React 19 + Vite landing page in `src/foqus-website/`. Waitlist form calling `POST /api/waitlist`. No auth. |
| Web app (`app.foqus.me`) | `[NEW]` | Does not exist. Must be created as a separate SPA. |
| WebAPI | `[EXISTS]` | Auth, classification, sessions, subscriptions slices implemented. No device endpoints. |
| Supabase auth | `[EXISTS]` | Configured for desktop app and extension. Web app needs callback URL registration. |
| Device management backend | `[NEW]` | No `Device` entity, no device endpoints. |

---

## Architecture Decision

`app.foqus.me` is a **separate SPA deployment** from the marketing site `foqus.me`:

| Property | Marketing Site (`foqus.me`) | Web App (`app.foqus.me`) |
|---|---|---|
| Purpose | Landing page, marketing, waitlist | Authenticated dashboard, analytics, settings |
| Stack | React 19, Vite | React 19, Vite (new project) |
| Auth | None | Supabase magic link |
| Hosting | Azure Static Web App | Azure Static Web App (separate) |
| Deployment | Separate CI/CD | Separate CI/CD |

The marketing site adds a "Sign in" link pointing to `app.foqus.me`.

---

## Deliverables

1. Azure Static Web App for `app.foqus.me`
2. CI/CD pipeline (GitHub Actions)
3. DNS and custom domain configuration
4. React app shell with routing
5. Supabase magic-link authentication
6. Account session management
7. Integrations page (connected devices, status, store links)
8. Backend `Devices` feature slice (register, heartbeat, list)
9. Settings/billing placeholder
10. Landing site integration (sign-in link)

---

## Detailed Tasks

### 1. Infrastructure

#### Azure Static Web App

- Provision a new Azure Static Web App for `app.foqus.me`.
- Configure custom domain and DNS:
  - CNAME record: `app.foqus.me` → Azure SWA default hostname.
  - SSL/TLS: managed by Azure (auto-provisioned).
- Environment separation:
  - **Production:** `app.foqus.me` → connects to `api.foqus.me`
  - **Staging:** `staging-app.foqus.me` or preview URLs → connects to staging API
- Environment variables:
  - `VITE_API_BASE_URL` — WebAPI base URL
  - `VITE_SUPABASE_URL` — Supabase project URL
  - `VITE_SUPABASE_ANON_KEY` — Supabase anonymous key
  - `VITE_PADDLE_ENVIRONMENT` — `sandbox` or `production`

#### CI/CD Pipeline

- GitHub Actions workflow:
  - Trigger on push to `main` (production) and PR (staging/preview).
  - Steps: install → lint → test → build → deploy to Azure SWA.
- Use `Azure/static-web-apps-deploy@v1` action.

#### Project Setup

- Create new project directory: `src/foqus-web-app/` (separate from `src/foqus-website/`).
- Stack: React 19, TypeScript, Vite 8 (matching existing conventions).
- Install dependencies:
  - `@supabase/supabase-js` — authentication
  - `react-router` — client-side routing
  - `@microsoft/signalr` — real-time hub connection (needed for Epic 5+6, but add now)
  - UI library — TBD (design decision: Tailwind CSS, shadcn/ui, or similar)

### 2. App Shell & Routing

#### Routes

| Route | Component | Auth Required | Description |
|---|---|---|---|
| `/` | Dashboard | Yes | Home page — summary + quick links |
| `/login` | Login | No | Magic link sign-in form |
| `/register` | Register | No | Registration (same magic-link flow, different messaging) |
| `/auth/callback` | AuthCallback | No | Supabase magic link callback handler |
| `/analytics` | AnalyticsPlaceholder | Yes | Placeholder → built in Epic 5 |
| `/integrations` | Integrations | Yes | Connected devices and status |
| `/settings` | Settings | Yes | Account settings |
| `/billing` | Billing | Yes | Subscription management |

#### Layout

- **Sidebar navigation:** Dashboard, Analytics, Integrations, Settings, Billing
- **Top bar:** User email/avatar, plan badge, sign out
- **Responsive:** Works on desktop and tablet

#### Protected Routes

- All routes except `/login`, `/register`, and `/auth/callback` require authentication.
- Unauthenticated users are redirected to `/login`.
- After login, redirect to `/` or the originally requested URL.

### 3. Authentication

#### Magic Link Sign-In

1. User enters email on `/login` page.
2. App calls `supabase.auth.signInWithOtp({ email })`.
3. User receives magic link email.
4. User clicks link → redirected to `/auth/callback` with token parameters.
5. `AuthCallback` component extracts session from URL and stores it.
6. Redirect to `/` (dashboard).

#### Registration

- Same flow as sign-in (Supabase auto-creates user on first magic link).
- `/register` uses different copy: "Create your account" vs "Sign in".

#### Session Management

- Use `supabase.auth.onAuthStateChange()` to track session state.
- Store session in memory (Supabase JS client handles this).
- On page load, call `supabase.auth.getSession()` to restore session.
- Token refresh handled by Supabase JS client automatically (per Epic 1 §10).
- On token refresh failure, redirect to `/login`.

#### Logout

- Call `supabase.auth.signOut()`.
- Clear local state.
- Redirect to `/login`.

#### Session Expiry

- If the refresh token is expired (7-day default), redirect to `/login` with a message.

### 4. API Client

Create a typed API client for the WebAPI:

```typescript
// Shared API client with auth interceptor
class FoqusApiClient {
  // Auth
  getMe(): Promise<UserProfile>

  // Devices
  registerDevice(device: RegisterDeviceRequest): Promise<Device>
  listDevices(): Promise<Device[]>
  deleteDevice(deviceId: string): Promise<void>

  // Subscriptions
  getSubscriptionStatus(): Promise<SubscriptionStatus>

  // Sessions (read-only from web app)
  listSessions(params: SessionListParams): Promise<PaginatedSessions>
  getSession(sessionId: string): Promise<Session>

  // Analytics (Epic 5)
  // getAnalyticsSummary(params): Promise<AnalyticsSummary>
  // getAnalyticsTrends(params): Promise<AnalyticsTrends>
}
```

- Attach Supabase access token as `Authorization: Bearer <token>` header.
- Handle 401 → trigger token refresh → retry once → redirect to login on failure.
- Handle 403 → show upgrade CTA (plan-gated endpoint).

### 5. Integrations Page

The integrations page is the web app's primary unique feature in this epic.

#### Layout

```
Integrations
├── Connected Devices
│   ├── Device Card (Windows App - "Work Laptop")
│   │   ├── Status: Online / Offline
│   │   ├── Last seen: 2 minutes ago
│   │   ├── Version: 1.2.0
│   │   ├── Platform: Windows 11
│   │   └── [Remove]
│   └── Device Card (Browser Extension - "Chrome")
│       ├── Status: Offline
│       ├── Last seen: 3 hours ago
│       ├── Version: 0.9.5
│       ├── Platform: Chrome 120
│       └── [Remove]
├── Add Integration
│   ├── Windows App → Download link / Store link
│   └── Browser Extension → Chrome Web Store link
└── Empty State
    └── "No integrations connected. Install the Windows app or browser extension to get started."
```

#### Data Source

- Call `GET /devices` to fetch the user's registered devices.
- Display each device with its type, name, status, last seen, version, platform.
- Status is derived from `LastSeenUtc` (online if < 3 minutes ago, offline otherwise).

#### Real-Time Updates (SignalR — optional for this epic, can be added in Epic 6)

- If SignalR is connected, listen for `DeviceOnline` / `DeviceOffline` messages to update status in real time.
- Otherwise, poll `GET /devices` every 30 seconds while the page is active.

#### Remove Device

- Call `DELETE /devices/{id}`.
- Remove from list.
- Confirm dialog before deletion.

### 6. Backend — Devices Feature Slice `[NEW]`

Create a new feature slice: `src/FocusBot.WebAPI/Features/Devices/`

#### Files

```
Features/Devices/
├── SLICE.md
├── DeviceEndpoints.cs
├── DeviceService.cs
├── DeviceDtos.cs
└── (Device entity in Data/Entities/)
```

#### Endpoints

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST /devices` | Yes | Register a new device |
| `GET /devices` | Yes | List user's devices |
| `GET /devices/{id}` | Yes | Get single device |
| `DELETE /devices/{id}` | Yes | Unregister a device |
| `PUT /devices/{id}/heartbeat` | Yes | Update heartbeat |

#### `POST /devices` — Register

**Request:**
```json
{
  "deviceType": "Desktop",
  "name": "Work Laptop",
  "fingerprint": "550e8400-e29b-41d4-a716-446655440000",
  "appVersion": "1.2.0",
  "platform": "Windows 11"
}
```

**Behavior:**
- If a device with the same `(userId, fingerprint)` already exists, update it instead of creating a duplicate.
- Return the device object with `id`.

**Response:** `201 Created` with device object.

#### `GET /devices` — List

- Returns all devices for the authenticated user.
- Includes computed `status` based on `LastSeenUtc`.

#### `PUT /devices/{id}/heartbeat`

- Updates `LastSeenUtc`, `AppVersion`, `Platform`.
- Returns `204 No Content`.
- Returns `404` if device not found or does not belong to user.

#### `DELETE /devices/{id}`

- Removes the device record.
- Returns `204 No Content`.
- Returns `404` if not found or not owned by user.

#### Entity

Per Epic 1 §4 — `Device` entity with `Id`, `UserId`, `DeviceType`, `Name`, `Fingerprint`, `AppVersion`, `Platform`, `Status`, `LastSeenUtc`, `CreatedAtUtc`, `UpdatedAtUtc`.

### 7. Settings Page (Placeholder)

Minimal settings page for initial release:

- Display user email
- Display current plan (Free / Cloud BYOK / Cloud Managed)
- "Change plan" link → opens Paddle customer portal or plan selection
- "Sign out" button

### 8. Billing Page (Placeholder)

- Display current subscription status
- If subscribed: plan name, renewal date, billing amount
- "Manage subscription" → Paddle customer portal URL
- If free: upgrade CTA with plan comparison

### 9. Dashboard (Placeholder)

- Welcome message with user name/email
- Current plan badge
- Quick stats (if synced data exists): sessions this week, total focused time
- Quick links: Integrations, Analytics, Settings
- If no integrations connected: "Get started" card with download links

### 10. Landing Site Integration

Update the marketing site (`src/foqus-website/`):

- Add "Sign in" link in the navigation bar → `https://app.foqus.me/login`
- Add "Get started" button on hero → `https://app.foqus.me/register`
- Keep the waitlist form for users who want updates but don't want to register yet

---

## Technical Notes

- Follow React conventions from the existing `foqus-website` project where applicable.
- Use TypeScript strict mode.
- Use environment variables for all external URLs (API, Supabase, Paddle).
- CORS: ensure the WebAPI allows `app.foqus.me` origin.
- `staticwebapp.config.json`: configure fallback to `index.html` for SPA routing.

---

## Exit Criteria

- [ ] `app.foqus.me` is live and accessible
- [ ] CI/CD deploys automatically on push to main
- [ ] Users can register and sign in via magic link
- [ ] Authenticated users see the dashboard, integrations, settings, and billing pages
- [ ] Integrations page shows connected devices with online/offline status
- [ ] Users can remove devices from the integrations page
- [ ] Backend `Devices` feature slice is implemented and tested
- [ ] Analytics page shows a placeholder with "Coming soon" messaging
- [ ] Marketing site links to the web app for sign-in/register
- [ ] Unauthenticated users are redirected to login
- [ ] Logout works and clears session

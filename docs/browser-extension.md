# Foqus Browser Extension

Chrome/Edge extension (Manifest V3) for real-time session tracking and task-alignment analysis within the browser. Classifies active tabs against the user's stated task, caches results, displays a distraction overlay, and syncs sessions across devices.

**Distribution**: Chrome Web Store / Edge Add-ons

---

## Architecture

```
┌────────────────────────────────────────────┐
│         UI Layer (React)                   │
│  ┌──────────┬────────────┬──────────────┐  │
│  │  Popup   │ Side Panel │   Options    │  │
│  │ AppShell │  AppShell  │ SettingsPage │  │
│  └──────────┴────────────┴──────────────┘  │
└──────────────────┬─────────────────────────┘
                   │ chrome.runtime messages
        ┌──────────▼──────────┐
        │  Runtime Layer      │
        │  background/index.ts│
        │  (~1174 lines)      │
        │  - Session state    │
        │  - Classification   │
        │  - SignalR hub      │
        │  - Client reg       │
        └──────────┬──────────┘
                   │
     ┌─────────────┼─────────────┐
     ▼             ▼             ▼
┌─────────┐  ┌──────────┐  ┌──────────────┐
│ Storage │  │Classifier│  │ Content      │
│ (chrome │  │ (OpenAI/ │  │ Script       │
│.storage)│  │  WebAPI) │  │ (overlay)    │
└─────────┘  └──────────┘  └──────────────┘
```

### Layers

1. **UI Layer (React)** — Popup, sidepanel, and options page share components (`ui/` folder)
2. **Runtime Layer** — Background service worker (`background/index.ts`) is the central orchestrator
3. **Shared Services** — 17 TypeScript modules under `shared/`
4. **Content Script** — Injected into tabs for distraction overlay display

---

## Shared Services (17 modules)

| Module | Purpose |
|---|---|
| `analytics.ts` | Session and daily analytics calculations |
| `apiClient.ts` | WebAPI HTTP client (sessions, clients, subscriptions, auth, classification) |
| `classifier.ts` | Page classification logic (BYOK direct to OpenAI, or managed via WebAPI) |
| `constants.ts` | Shared constants |
| `extensionPresence.ts` | Desktop ↔ extension local WebSocket presence |
| `focusbotAuth.ts` | Supabase token storage (`chrome.storage.local`) |
| `integrationTypes.ts` | Integration state types |
| `metrics.ts` | Live summary + session summary calculations |
| `runtime.ts` | Runtime message helpers (UI ↔ background) |
| `signalr.ts` | SignalR hub connection for cross-device sync |
| `storage.ts` | `chrome.storage.local` CRUD |
| `subscription.ts` | Plan type mapping between server and client enums |
| `supabaseClient.ts` | Supabase client initialization (localStorage persistence adapter) |
| `types.ts` | Core types: `FocusSession`, `InProgressVisit`, `Settings`, `RuntimeState` |
| `url.ts` | URL parsing, domain extraction, trackability check |
| `utils.ts` | ID generation, timestamps, `APP_KEYS` constants |
| `webAppUrl.ts` | Web app URL builder (billing, analytics links) |

---

## UI Entry Points

| Entry | Path | Purpose |
|---|---|---|
| **Popup** | `popup/index.html` → `popup/main.tsx` | Toolbar popup — session card, start/end, live status |
| **Side Panel** | `sidepanel/index.html` → `sidepanel/main.tsx` | Chrome side panel — same as popup but persistent |
| **Options** | `options/index.html` → `options/main.tsx` | Settings, account, subscription management |
| **Content Script** | `content/index.ts` | Distraction overlay injected into tabs |
| **Auth Callback** | `auth-callback.html` → `auth-callback/main.ts` | Magic-link callback (parses tokens from URL hash) |

### Shared UI Components (`ui/`)

| Component | Purpose |
|---|---|
| `AppShell.tsx` | Main shell for popup/sidepanel — session card, trial banner, BYOK prompt |
| `SessionCard.tsx` | Active session display: task, timer, classification status, controls |
| `SummaryCard.tsx` | Session metrics: focus %, aligned/distracted time, top domains |
| `BYOKInfoDialog.tsx` | API key security info dialog |
| `useRuntimeState.ts` | React hook — subscribes to background state changes |
| `styles.css` | Shared UI styles |

---

## Background Service Worker

`background/index.ts` (~1174 lines) is the central state manager and orchestrator.

### Key Responsibilities

- **Session lifecycle**: Start/end/pause/resume via WebAPI + local state management
- **Classification orchestration**: Classify active tab on navigation/title change, cache results
- **SignalR hub**: Connect for cross-device sync (session events, classification broadcast, plan changes)
- **Client registration**: Fingerprint-based registration with WebAPI, stored in `chrome.storage.local`
- **Badge/icon management**: Live focus score on badge, color-coded extension icon
- **Idle detection**: `chrome.idle` API — auto-pause on 5 min idle, auto-resume on active
- **Distraction alerts**: Send to content script for overlay display
- **State reconciliation**: Sync local state with cloud on reconnection

### Exclusive State Mutations

All state mutations are serialized via `runExclusive()` promise queue:
- Prevents race conditions from simultaneous tab events
- Ensures cache and state are always consistent
- Critical for multi-tab sessions

---

## State Management

### Runtime State

All state stored in `chrome.storage.local`:

```typescript
interface RuntimeState {
  settings: Settings;            // User config (plan, API key, model, subscription info)
  activeSession: FocusSession | null; // Current session (if any)
  lastSummary: SessionSummary | null;  // Last completed session summary
  lastError: string | null;      // Last error message
  isAuthenticated: boolean;      // Whether user is signed in
}
```

### Storage Keys

| Key | Type | Purpose |
|---|---|---|
| `focusbot.settings` | `Settings` | User configuration (plan, API key, model, subscription status) |
| `focusbot.activeSession` | `FocusSession` | Current active session |
| `focusbot.sessions` | `FocusSession[]` | Historical completed sessions |
| `focusbot.completedSessions` | `CompletedSession[]` | Completed session summaries |
| `focusbot.classificationCache` | `Record<string, CacheEntry>` | Classification cache |
| `focusbot.lastSummary` | `SessionSummary` | Last completed session summary |
| `focusbot.lastError` | `string` | Last error message |
| `focusbot.clientFingerprint` | `string` | Stable client fingerprint |
| `focusbot.clientId` | `string` | Server-assigned client ID |
| `focusbot.offlineQueue` | `QueueEntry[]` | Offline request queue |
| `focusbot.deviceFingerprint` | `string` | *(deprecated)* Legacy fingerprint key |
| `focusbot.deviceId` | `string` | *(deprecated)* Legacy device ID key |

### Active Session State

```typescript
interface FocusSession {
  sessionId: string;
  taskText: string;
  taskHints?: string;             // Optional task hints/context
  startedAt: string;              // ISO timestamp
  endedAt?: string;               // ISO timestamp (set when session ends)
  visits: PageVisit[];            // Completed page visits
  summary?: SessionSummary;       // Summary (set when session ends)
  currentVisit?: InProgressVisit;
  lastHubClassification?: HubClassificationSnapshot; // Latest classification from SignalR
  pausedAt?: string;              // Pause timestamp
  totalPausedSeconds?: number;    // Accumulated pause time
  pausedBy?: "user" | "idle";     // Who triggered pause
}
```

### Page Visit State

```typescript
interface InProgressVisit {
  visitToken: string;              // Unique ID
  tabId: number;                   // Chrome tab ID
  url: string;
  domain: string;
  title: string;
  enteredAt: string;               // ISO timestamp
  visitState: "classifying" | "classified" | "error";
  classification?: "aligned" | "neutral" | "distracting";
  confidence?: number;
  reason?: string;
  score?: number;
  reusedClassification?: true;     // Whether result came from cache
}
```

---

## Classification

### Dual Mode

| Mode | Setting | Data Path |
|---|---|---|
| **BYOK** | `plan: "cloud-byok"` (with own API key) | Extension → OpenAI directly (`POST /v1/chat/completions`) |
| **Managed** | `plan: "cloud-managed"` | Extension → WebAPI `POST /classify` → LLM |

### BYOK Classification

- Models: `gpt-4o-mini` (default), `gpt-5-mini`
- 3 retry attempts with exponential backoff (0ms, 300ms, 600ms)
- 8-second timeout per attempt
- Returns: `aligned`, `neutral`, or `distracting` + confidence + reason

### Managed Classification

- WebAPI `POST /classify` with `Authorization: Bearer <Supabase access_token>`
- Returns: `{ score: 1-10, reason, cached }` → mapped to: score > 5 = Aligned, 5 = Neutral, < 5 = Distracted
- Requires active subscription or trial (402 when expired)

### Classification Cache

- **Key**: `SHA256(taskText.toLowerCase() + "::" + url.toLowerCase())`
- **Storage**: IndexedDB (persistent across sessions)
- **Hit behavior**: Instant return, no API call
- **TTL**: None (manual clear only — clear extension data or classification cache to reset)

### Classification Triggers

Classification runs automatically when:
1. Session is active (not paused)
2. User navigates to a new URL
3. Page title changes
4. Tab is activated

URL must be "trackable" (not `chrome://`, `about:`, etc. — see `url.ts`).

### Excluded Domains

User can exclude domains from classification. Excluded domains are auto-classified as "aligned" with confidence 1.0. Useful for always-work sites like `github.com`.

---

## Session Lifecycle

### Starting a Session

1. User enters task text in popup
2. Validate: task not empty, no active session, API key configured (BYOK) or signed in (managed)
3. Call `POST /sessions` via WebAPI
4. Create `FocusSession` locally with task text and timestamp
5. Capture current active tab, begin classification
6. Broadcast state update to all UI views

### During a Session

Background monitors tab events:
- `chrome.tabs.onActivated` — tab switch
- `chrome.tabs.onUpdated` — URL/title change
- `chrome.windows.onFocusChanged` — window focus

For each event: check trackability → check cache → classify → update state → alert content script if distracting.

### Pause / Resume

- **Manual pause**: User clicks Pause → `POST /sessions/{id}/pause` → freeze metrics
- **Idle pause**: `chrome.idle` detects 5 min inactivity → auto-pause (`pausedBy: "idle"`)
- **Resume**: `POST /sessions/{id}/resume` → re-classify current tab
- Only idle-paused sessions auto-resume when user becomes active
- `totalPausedSeconds` accumulates all pause intervals

### Ending a Session

1. If paused, add final pause interval to `totalPausedSeconds`
2. Finalize current visit
3. Calculate `SessionSummary` (focus %, distraction count, tracked time, context switch cost, top domains)
4. Call `POST /sessions/{id}/end` with summary
5. Save session to history, save summary for display
6. Clear active session, broadcast state update

---

## Authentication (Foqus Account)

### Sign-In Flow

1. Options page: user enters email, clicks "Send magic link"
2. Extension calls `supabase.auth.signInWithOtp({ email })`
3. Supabase sends magic-link email, redirect target = `chrome.runtime.getURL("src/options/index.html")`
4. User clicks link → `auth-callback.html` parses hash params (`access_token`, `refresh_token`)
5. Tokens relayed to background SW via `chrome.runtime.sendMessage`
6. Stored in `chrome.storage.local`
7. Background calls `GET /auth/me` → auto-provisions user + creates 24h trial

### Token Management

- Tokens stored in `chrome.storage.local` (not Supabase client's localStorage — background SW has no DOM)
- Token refresh: manual REST call to Supabase `/auth/v1/token`
- Sign-out: clears tokens, client fingerprint, email from storage

---

## SignalR Integration

When signed in with Foqus account, background SW connects to SignalR hub for cross-device sync:

| Event | Handler |
|---|---|
| `SessionStarted` | Reconcile: adopt session locally, sync state |
| `SessionEnded` | Clear local session |
| `SessionPaused` / `SessionResumed` | Mirror if matching current session |
| `ClassificationChanged` | Update current visit if from another device source |
| `PlanChanged` | Refresh subscription status |

Connection uses JWT via `access_token` query parameter. Auto-reconnects with backoff.

---

## Client Registration

- **Fingerprint**: Generated on first install, stored in `chrome.storage.local`
- **Register**: `POST /clients` with fingerprint, `ClientType.Extension`, host (`Chrome` or `Edge`)
- **Heartbeat**: Periodic `PUT /clients/{id}/heartbeat`
- Desktop app key does NOT sync to extension (per-client storage)

---

## Extension Presence (Desktop Integration)

When desktop app is running on the same machine:
- Extension connects to `ws://localhost:9876/foqus-presence` for presence signaling
- Desktop skips browser window classification when extension is online

See [docs/extension-presence-protocol.md](extension-presence-protocol.md) and [docs/integration.md](integration.md).

---

## Distraction Overlay

Content script (`content/index.ts`) injected into all tabs:
- Displays overlay when current page is classified as "distracting"
- Semi-transparent dark overlay with distraction message
- Hidden when session is paused
- Removed when navigating to an aligned page or ending session

---

## Trial and Subscription UX

### Trial Banner (Popup)

- Compact, non-dismissible banner in `AppShell` popup
- Shown for Foqus trial (`status = trial`, `planType = 0`, future `trialEndsAt`)
- **Manage plan** links to `https://app.foqus.me/billing`
- Hidden in sidepanel

### Options Page Subscription UI

- Subscription summary: current plan label, end date, manage link, refresh action
- After trial expiry without paid subscription: "No active plan" with "View plans" link

### BYOK Prompt

When plan is `cloud-byok` and no API key in settings:
- Popup `AppShell` shows "API key required" banner with **Learn more** and **Open settings**
- `BYOKInfoDialog` explains: keys are per-client, stored in extension storage, sent directly to AI provider over HTTPS, never to Foqus servers
- Options page highlights API key field with security messaging

---

## UI Components

### SessionCard

| State | Display |
|---|---|
| Paused | "Paused" or "Paused (idle)" (when `pausedBy === "idle"`) |
| Classifying | "Analyzing page..." |
| Aligned | "Aligned" + reason |
| Distracting | "Distracting" + reason |
| Error | "Classifier error" |
| No visit | "Waiting for signal" |

Controls: **Pause**/**Resume** + **End Task**

### SummaryCard

- **During session**: Live-updating "Current Session" metrics (refreshes every second)
- **Between sessions**: "Latest Session Summary" from last completed session
- Metrics: focus %, distraction count, tracked time, context switch cost, top distracting domains

### Side Panel Tint

When classification is "Distracting", `AppShell` applies a subtle red background tint with pulse animation. Disabled for `prefers-reduced-motion: reduce`. Not shown when paused or classifying.

### Settings Page

| Section | Mode | Controls |
|---|---|---|
| Account mode | All | Toggle: BYOK / Foqus account |
| API key | BYOK only | Text input + validate button |
| Model selection | BYOK only | Dropdown: `gpt-4o-mini` (default) |
| Foqus account | Foqus account | Email input, send magic link, sign-in status, sign out |
| Excluded domains | All | Domain list management |
| Subscription | Foqus account | Plan summary, manage link |

---

## Analytics

### Session Summary Calculations

`calculateSessionSummary()` for completed sessions, `calculateLiveSummary(session)` for active sessions:

```typescript
interface SessionSummary {
  taskName: string;
  totalSessionSeconds: number;       // Wall-clock minus paused time
  totalTrackedSeconds: number;       // Total classified visit time
  alignedSeconds: number;
  distractingSeconds: number;
  focusPercentage: number;
  distractionCount: number;
  contextSwitchCostSeconds: number;
  topDistractionDomains: DomainAggregate[];
  topAlignedDomains: DomainAggregate[];
}
```

### Daily Analytics

Available for Today / Last 7 days / Last 30 days. Aggregates: total sessions, tracked seconds, aligned/distracting time, distraction count, focus percentage, top domains.

---

## Performance

| Optimization | Detail |
|---|---|
| Classification cache | SHA-256 key → IndexedDB, instant cache hits |
| Request batching | Only latest navigation triggers classification; previous in-flight ignored |
| Exclusive state mutations | `runExclusive()` queue prevents race conditions |
| Retry with backoff | 3 attempts: 0ms, 300ms, 600ms delay, 8s timeout each |

---

## Manifest Permissions

| Permission | Justification |
|---|---|
| `storage` | User settings, session data, classification cache |
| `tabs` | Active tab URL/title for classification |
| `sidePanel` | Side panel UI |
| `alarms` | Periodic tasks (heartbeat, token refresh) |
| `idle` | Detect user inactivity for auto-pause |
| `host_permissions: <all_urls>` | Classify any website against user's task; send to OpenAI API or Foqus WebAPI |

---

## Privacy

- **BYOK mode**: API key stored in `chrome.storage.local` (only extension can access). Sent directly to OpenAI over HTTPS. Never sent to Foqus servers.
- **Foqus account mode**: Page URL/title sent to Foqus WebAPI `POST /classify` for classification. Supabase access token stored locally.
- **No data selling/transfer**: Data used solely for focus classification.
- **Data retention**: All local data in `chrome.storage.local`. User can clear at any time.

---

## Building and Testing

```bash
# Install
cd browser-extension && npm install

# Dev (hot reload)
npm run dev

# Build (development)
npm run build

# Build (production, includes icon resize)
npm run build:prod

# Pack for store submission
npm run pack

# Tests (~83 tests, Vitest)
npm test

# Watch mode
npm test:watch
```

### Loading in Browser

1. `npm run build`
2. Open `chrome://extensions` or `edge://extensions`
3. Enable Developer mode
4. Load unpacked from `browser-extension/dist`

### Key Dependencies

| Package | Purpose |
|---|---|
| `@microsoft/signalr` | Cross-device sync |
| `@supabase/supabase-js` | Foqus account auth |
| `react` / `react-dom` | UI components |
| `@crxjs/vite-plugin` | Manifest V3 Vite build |
| `vitest` | Test runner |

---

## Known Limitations

1. No iframe support — extension cannot access iframe content
2. Cache never expires — user must manually clear to reset
3. Single session only — one active session per browser profile
4. Service worker may unload — restarts on next event, state persisted in `chrome.storage.local`
5. API key not encrypted at rest — standard browser extension limitation (no DPAPI equivalent)

# FocusBot Browser Extension - Technical Documentation

## Overview

The FocusBot Browser Extension is a standalone Chrome/Edge extension that enables **real-time session tracking and task-alignment analysis** directly within your web browser. Unlike the desktop app which monitors all Windows applications, the extension focuses specifically on web browsing behavior.

**Target Use Case:** Users who want to stay aligned with a single stated task while browsing—whether work (e.g. code review, research, marketing, documentation) or breaks (e.g. watch movies, browse social). Classification is based only on whether the current page matches the user's task, not on productivity or "deep work" norms.

---

## Architecture

### Component Overview

```
┌─────────────────────────────────────────────┐
│         UI Layer (React)                    │
│  ┌──────────────┬──────────────────────┐   │
│  │   Popup      │   Side Panel / Tab   │   │
│  │ SessionCard  │   Analytics          │   │
│  │ SummaryCard  │   SettingsPage       │   │
│  └──────────────┴──────────────────────┘   │
└──────────────────┬──────────────────────────┘
                   │
        ┌──────────▼──────────┐
        │  Runtime Layer      │
        │  background/index.ts│
        │  - Session state    │
        │  - AI classification│
        │  - Analytics        │
        └──────────┬──────────┘
                   │
     ┌─────────────┼─────────────┐
     ▼             ▼             ▼
┌─────────┐  ┌──────────┐  ┌──────────────┐
│ Storage │  │Classifier│  │ Content      │
│(IndexDB)│  │ (OpenAI) │  │ Script       │
└─────────┘  └──────────┘  └──────────────┘
```

### Key Layers

1. **UI Layer (React)**
   - Popup view: Main focus session card
   - Side panel: Detailed analytics and settings
   - Components are re-usable across popup, sidepanel, and analytics page

2. **Runtime Layer (background/index.ts)**
   - Central state management
   - Handles all runtime requests from UI
   - Manages session lifecycle
   - Orchestrates page classification
   - Broadcasts state updates to all UI views

3. **Shared Services**
   - `classifier.ts`: OpenAI API integration with caching and retry logic
   - `runtime.ts`: Message passing between UI and background
   - `storage.ts`: IndexedDB persistence layer
   - `analytics.ts`: Focus metrics aggregation
   - `metrics.ts`: Session summary calculations

4. **Content Script (content/index.ts)**
   - Injected into every tab
   - Receives distraction alerts from background
   - Displays distraction overlay when classified as distracting

---

## State Management

### Runtime State Structure

All state is stored in IndexedDB and managed through `RuntimeState`:

```typescript
interface RuntimeState {
  settings: Settings;           // User config (API key, excluded domains)
  activeSession: FocusSession | null;  // Current session data
  lastSummary: SessionSummary | null;  // Summary from last completed session
  lastError: string | null;            // Last error message
}
```

### Active Session State

While a session is active:

```typescript
interface FocusSession {
  sessionId: string;
  taskText: string;
  startedAt: string;           // ISO timestamp
  endedAt?: string;            // Undefined until session ends
  visits: PageVisit[];         // Completed visits
  summary?: SessionSummary;    // Computed when session ends
  currentVisit?: InProgressVisit;  // Current page being tracked
}
```

### Page Visit Lifecycle

Each page visit goes through these states:

```typescript
interface InProgressVisit {
  visitToken: string;          // Unique ID for this visit
  tabId: number;               // Browser tab ID
  url: string;
  domain: string;              // Extracted from URL
  title: string;               // Page title
  enteredAt: string;           // ISO timestamp when user navigated here
  visitState: "classifying" | "classified" | "error";
  classification?: "aligned" | "distracting";
  confidence?: number;         // 0-1 confidence score
  reason?: string;             // Why classified as aligned/distracting
}
```

**State Transitions:**

1. **User navigates to a page**
   - `visitState: "classifying"` (UI shows "Analyzing page...")
   - Background calls OpenAI API in parallel

2. **Classification succeeds**
   - `visitState: "classified"`
   - `classification` and `reason` populated
   - UI shows "Aligned" or "Distracting"
   - Distraction alert sent to content script

3. **Classification fails**
   - `visitState: "error"`
   - `reason` contains error message
   - UI shows "Classifier error"
   - Page is not recorded as a visit

4. **User navigates away**
   - If `visitState === "classified"`, current visit is finalized and added to `visits[]`
   - If `visitState !== "classified"`, current visit is discarded (no history)
   - `currentVisit` is cleared

---

## Classification Flow

### Triggering Classification

Classification is triggered automatically when:
1. Session is active
2. User navigates to a new URL
3. Page title changes

The URL must be "trackable" (see `url.ts` for domain whitelist).

### Classification Process

```
┌─────────────────────────────────┐
│ Page navigation detected         │
│ (onActivated / onUpdated event)  │
└────────────────┬────────────────┘
                 │
         ┌───────▼────────┐
         │ Check cache    │
         │ (task + URL)   │
         └───────┬────────┘
                 │
         ┌───────▼────────────────────┐
         │ Cache hit?                 │
         │ ├─ YES: return cached      │
         │ └─ NO: check policy        │
         └───────┬────────────────────┘
                 │
         ┌───────▼──────────────────┐
         │ Policy checks            │
         │ 1. Is domain excluded?   │
         │ 2. Is known distraction? │
         └───────┬──────────────────┘
                 │
         ┌───────▼─────────────────────────┐
         │ Call OpenAI API (with retries)  │
         │ - 3 attempts                     │
         │ - 300-600ms backoff              │
         │ - 8 second timeout               │
         └───────┬─────────────────────────┘
                 │
         ┌───────▼──────────────────┐
         │ Cache result             │
         │ Save to IndexedDB         │
         └───────┬──────────────────┘
                 │
         ┌───────▼──────────────────┐
         │ Broadcast state update   │
         │ Send distraction alert   │
         └──────────────────────────┘
```

### No Pre-classified Domains

All pages are classified based on the user's specific task, not on a hardcoded list. This ensures that sites like Facebook, YouTube, or Reddit are classified as "aligned" if the user's task is explicitly to use them (e.g., "Review Facebook feed" or "Watch tutorial video"), and "distracting" if they visit them during unrelated tasks.

### Excluded Domains (User Configuration)

User can exclude domains from classification entirely. Excluded domains are automatically classified as "aligned" with confidence 1.0.

**Example:** A developer might exclude `github.com` and `stackoverflow.com` if those are work-related.

---

## AI Classification

### OpenAI API Call

```javascript
POST https://api.openai.com/v1/chat/completions

{
  "model": "gpt-4o-mini",  // configurable
  "temperature": 0.1,      // low variance
  "response_format": { "type": "json_object" },
  "messages": [
    {
      "role": "system",
      "content": "You decide whether the current page matches the user's stated task..."
    },
    {
      "role": "user",
      "content": "Task: Learn how GitHub works\nURL: https://github.com/...\nTitle: GitHub: Where the world builds software"
    }
  ]
}
```

### Expected Response

```json
{
  "classification": "aligned",
  "confidence": 0.95,
  "reason": "GitHub is directly relevant to learning how Git and version control work."
}
```

### Parsing & Fallback

If the API returns malformed JSON:
- Fallback parser checks if response contains "distracting" (case-insensitive)
- Defaults to "aligned" if neither keyword found
- Sets confidence to 0.5 and reason to "Fallback parser used..."

---

## Performance Optimizations

### 1. Classification Cache

**Cache Key:** `SHA256(taskText.toLowerCase() + "::" + url.toLowerCase())`

**Storage:** IndexedDB (persistent across sessions)

**Cache Hit Behavior:** 
- Instant return, no API call
- Eliminates latency when revisiting same URL

**Example:**
```
Cache Miss (first visit to github.com): ~1-2 seconds API latency
Cache Hit (return to github.com): instant
```

**Cache Invalidation:** Manual only (no TTL). User must clear extension data to reset. After changing classifier prompt logic, clear extension data (or the classification cache) so existing cached results are replaced with new classifications.

### 2. Request Batching

The extension handles rapid page navigations gracefully:
- Only the latest navigation triggers a classification request
- Previous in-flight requests for the same session are ignored
- Uses `visitToken` to validate that classification result matches the current visit

### 3. Retry Logic with Exponential Backoff

```
Attempt 1: Immediate
Attempt 2: Wait 300ms, then retry
Attempt 3: Wait 600ms, then retry
Timeout: 8 seconds per attempt
```

If all 3 attempts fail: `visitState: "error"`, page is not recorded.

### 4. Exclusive State Mutations

All state mutations are serialized via `runExclusive()` promise queue:
- Prevents race conditions from simultaneous tab events
- Ensures cache and state are always consistent
- Critical for multi-tab sessions

---

## Session Lifecycle

### Starting a Session

```
1. User enters task text in popup
2. Validate: task is not empty
3. Validate: no active session exists
4. Validate: OpenAI API key is configured
5. Create FocusSession with taskText and startedAt timestamp
6. Capture current active tab
7. Broadcast state update
8. Begin classification on current tab
```

### During Session

```
- Background monitors tab events:
  - chrome.tabs.onActivated
  - chrome.tabs.onUpdated (when URL or title changes)
  - chrome.windows.onFocusChanged
  
- For each tab event:
  - Check if URL is trackable
  - Compare to currentVisit (avoid re-classifying same page)
  - Finalize previous visit if needed
  - Create new InProgressVisit
  - Start classification
```

### Ending Session

```
1. User clicks "End Task" button
2. Finalize current visit
3. Calculate SessionSummary:
   - totalSessionSeconds: wall-clock duration
   - totalTrackedSeconds: sum of all completed visits
   - alignedSeconds: sum of visits with classification "aligned"
   - distractingSeconds: sum of visits with classification "distracting"
   - distractionCount: number of transitions to "distracting" state
   - focusPercentage: (alignedSeconds / totalTrackedSeconds) * 100
   - contextSwitchCostSeconds: sum of visit durations < 2 minutes
   - topDistractionDomains: sorted list of distracting domains
   - topAlignedDomains: sorted list of aligned domains

4. Save session to history
5. Save summary for display
6. Clear active session
7. Broadcast state update
```

---

## Analytics

### Session Summary Calculations

`calculateSessionSummary()` is used for completed sessions; `calculateLiveSummary(session)` produces the same `SessionSummary` shape for an active session by including completed visits plus the current visit with duration up to "now" (for live UI updates).

```typescript
interface SessionSummary {
  taskName: string;
  totalSessionSeconds: number;        // Wall-clock time
  totalTrackedSeconds: number;        // Sum of visits with classification
  alignedSeconds: number;             // Visits classified "aligned"
  distractingSeconds: number;         // Visits classified "distracting"
  distractionCount: number;           // Number of distraction episodes
  focusPercentage: number;            // alignedSeconds / totalTrackedSeconds * 100
  contextSwitchCostSeconds: number;   // Time spent in visits < 2 minutes
  topDistractionDomains: DomainAggregate[];
  topAlignedDomains: DomainAggregate[];
}

interface DomainAggregate {
  domain: string;
  totalSeconds: number;
  visitCount: number;
}
```

### Daily Analytics

Analytics available for:
- **Today** - from midnight local time
- **Last 7 days** - rolling window
- **Last 30 days** - rolling window

Each period aggregates:
- Total sessions
- Total tracked seconds
- Total aligned/distracting seconds
- Distraction count
- Average context switch cost
- Focus percentage
- Top distracting/aligned domains

---

## UI Components

### SessionCard (Popup View)

Displays current session status and controls:
- Task name and elapsed time
- Current page classification (Aligned / Distracting / Analyzing page...)
- AI reason for classification
- Start/End session buttons

**Status Badges:**
- "Analyzing page..." - `visitState: "classifying"`
- "Aligned" - `classification: "aligned"`
- "Distracting" - `classification: "distracting"`
- "Classifier error" - `visitState: "error"`
- "Waiting for signal" - No currentVisit (shouldn't happen in normal flow)

### SummaryCard (Session Metrics)

- **When a session is active:** Shows **Current Session** with live-updating metrics (focus %, aligned/distracting time, context switch cost, distraction count, tracked time, top distracting domains). Values refresh every second as the current page visit duration grows.
- **When no session is active:** Shows **Latest Session Summary** from the last completed session, or an empty state if no session has been completed yet.
- Same metrics in both modes: focus percentage, distraction count, total tracked time, context switch cost, top distracting domains.

### AnalyticsPage (Full View)

Detailed analytics dashboard:
- Daily focus score buckets
- Trend charts for 7d/30d
- Top websites breakdown
- Recent sessions list

### SettingsPage

Configuration:
- OpenAI API key (encrypted in storage)
- Model selection (dropdown)
- Excluded domains (list add/remove)

---

## Storage

### IndexedDB Schema

**Stored Objects:**

| Key | Type | Purpose |
|-----|------|---------|
| `focusbot.settings` | Settings | User configuration |
| `focusbot.activeSession` | FocusSession | Current session (if any) |
| `focusbot.sessions` | FocusSession[] | Historical sessions |
| `focusbot.classificationCache` | Record<string, CacheEntry> | Page classification cache |
| `focusbot.lastSummary` | SessionSummary | Last completed session summary |
| `focusbot.lastError` | string | Last error message |

### Data Persistence

- All data is stored locally in IndexedDB
- No cloud sync
- No telemetry
- No data shared beyond API calls to OpenAI

### Cache Entry Structure

```typescript
interface CacheEntry {
  classification: "aligned" | "distracting";
  confidence: number;         // 0-1
  reason?: string;
  createdAt: string;          // ISO timestamp
}
```

---

## Error Handling

### Classification Errors

**Timeout (8 seconds):**
- Thrown after all 3 retry attempts fail
- Message: "Classification timed out. Please verify network connectivity and model access."
- Visit is marked as error, not recorded

**Invalid API Key:**
- HTTP 401 from OpenAI
- Message: "OpenAI classification failed (401)."
- Suggests checking API key in settings

**Rate Limited:**
- HTTP 429 from OpenAI
- Retried automatically with backoff
- If all retries fail, error message includes "(429)"

**API Unavailable:**
- HTTP 503 from OpenAI
- Retried automatically
- Error: "OpenAI classification failed (503)."

### Unhandled Errors

All errors are:
1. Logged to `lastError` in storage
2. Broadcast to all UI views
3. Displayed in error message section of UI
4. Clearable by user clicking "Clear error"

---

## Debugging

### Viewing Runtime State

In the background service worker console (chrome://extensions → Service Worker):

```javascript
// View all stored state
chrome.storage.local.get(null, (items) => {
  console.log(items);
});

// Check for errors
chrome.storage.local.get('focusbot.lastError', (items) => {
  console.log("Last error:", items['focusbot.lastError']);
});

// View current session
chrome.storage.local.get('focusbot.activeSession', (items) => {
  const session = items['focusbot.activeSession'];
  console.log("Current visit state:", session?.currentVisit?.visitState);
  console.log("Classification:", session?.currentVisit?.classification);
});

// View cache stats
chrome.storage.local.get('focusbot.classificationCache', (items) => {
  const cache = items['focusbot.classificationCache'];
  console.log("Cache entries:", Object.keys(cache).length);
});
```

### Network Debugging

In the background service worker DevTools (chrome://extensions → Service Worker → DevTools):

1. Open **Network** tab
2. Navigate to a new page during a session
3. Look for requests to `https://api.openai.com/v1/chat/completions`
4. Check response time and status code
5. Inspect response body for errors

---

## Known Limitations

1. **No iframe support** - Extension cannot access content inside iframes
2. **Cache never expires** - User must manually clear extension data to reset cache
3. **Single session only** - Only one active session per browser profile
4. **No background persistence** - Service worker may unload; background script restarts on next event
5. **API key in client** - API key is obfuscated but not truly secure (for BYOK mode only)

---

## Future Enhancements

1. **Sync with Desktop App** - Import/export sessions between browser extension and desktop app
2. **Real-time Cloud Sync** - Sync sessions across devices
3. **Browser Profiles** - Separate settings per browser profile
4. **Advanced Analytics** - Heatmaps, focus trends, recommended break times
5. **Focus Music Integration** - Suggest focus playlists during sessions
6. **Slack Integration** - Post focus summary to Slack
7. **Cache TTL** - Automatic cache expiration (e.g., 7 days)
8. **Rate Limiting** - Client-side rate limiting for API quota management

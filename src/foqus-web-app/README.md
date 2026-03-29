# Foqus web app

React (Vite) client for account management, billing, and cross-device focus analytics. Uses Supabase auth and the Foqus Web API.

## Pages

- **Dashboard** (`/`): Today's KPIs (focus score ring, deep work, sessions, distractions), expandable list of completed sessions today, and **session controls** (start / pause / resume / end). Polling refreshes the active session every 5 seconds while one is in progress.
- **Analytics** (`/analytics`): Period summaries (7 / 30 / 90 days), focus trend chart, device breakdown, and paginated session history with focus-score coloring.
- **Billing** (`/billing`): Current subscription status and paid plan cards (Foqus BYOK / Foqus Premium). No free tier. New users get a 24h full-access trial auto-created on first `GET /subscriptions/status` call.

Sessions you **start and end only in the web app** are stored with **no distraction classification** (the web app sends full focused time and 100% focus score on end). For alignment metrics from the classifier, use the Windows app or browser extension.

## Trial and subscription UX

- **Auto-trial**: On first authenticated `GET /subscriptions/status`, the API auto-creates a 24h `TrialFullAccess` trial. No client-side `POST /trial` call needed.
- **Welcome modal** (`TrialWelcomeModal`): Shown once on first trial visit (dismissed state persisted in `localStorage` per user). Explains Foqus + trial duration + link to `/billing`.
- **Trial banner** (in `Layout`): Purple countdown banner shown above all pages when trial is active. Shows remaining time and a "Choose a plan" link.
- **`SubscriptionContext`**: Wraps the protected layout; fetches subscription status once per session and provides `{ subscription, loading, error, refresh }` to all pages.

## Scripts

- `npm run dev` — dev server (port 5174)
- `npm run build` — typecheck + production build
- `npm run test` — Vitest unit and component tests (formatting, analytics math, API client, pages)
- `npm run lint` — ESLint

Local dev needs `.env` with `VITE_SUPABASE_URL` and `VITE_SUPABASE_ANON_KEY`. Optional: `VITE_API_BASE_URL` (defaults to `http://localhost:5251` in dev).

export type Classification = "aligned" | "neutral" | "distracting";

export interface ClassificationResult {
  classification: Classification;
  confidence: number;
  reason?: string;
  score?: number;
}

/**
 * Plan tier for the signed-in user. All users must have an account;
 * there is no anonymous usage.
 *
 * - trial: 24-hour full trial. User provides their own API key.
 * - cloud-byok: User provides their own API key + full cloud analytics and sync.
 * - cloud-managed: Platform provides the API key. Full cloud analytics and sync.
 */
export type PlanType = "trial" | "cloud-byok" | "cloud-managed";

/** Returns true when the plan uses a user-supplied OpenAI API key. */
export const planRequiresApiKey = (plan: PlanType): boolean => plan !== "cloud-managed";

/** Returns true when classification routes directly to the LLM provider (not via POST /classify). */
export const planUsesDirectClassification = (plan: PlanType): boolean => plan !== "cloud-managed";

export interface Settings {
  /** Active plan tier. Requires a signed-in account for all values. */
  plan: PlanType;
  /** User-provided OpenAI API key when plan is trial or cloud-byok. */
  openAiApiKey: string;
  classifierModel: string;
  onboardingCompleted: boolean;
  subscriptionStatus?: string;
  serverPlanType?: 0 | 1 | 2;
  trialEndsAt?: string;
  currentPeriodEndsAt?: string;
  /** Email of the signed-in Foqus account. */
  focusbotEmail?: string;
}

export interface PageVisit {
  pageVisitId: string;
  sessionId: string;
  tabId: number;
  url: string;
  domain: string;
  title: string;
  enteredAt: string;
  leftAt: string;
  durationSeconds: number;
  classification: Classification;
  confidence: number;
  reason?: string;
  score?: number;
}

export interface SessionSummary {
  taskName: string;
  totalSessionSeconds: number;
  totalTrackedSeconds: number;
  alignedSeconds: number;
  distractingSeconds: number;
  distractionCount: number;
  focusPercentage: number;
  contextSwitchCostSeconds: number;
  topDistractionDomains: DomainAggregate[];
  topAlignedDomains: DomainAggregate[];
}

/** Completed session with only summary persisted; visits are not stored. */
export interface CompletedSession {
  sessionId: string;
  taskText: string;
  taskHints?: string;
  startedAt: string;
  endedAt: string;
  summary: SessionSummary;
}

export interface FocusSession {
  sessionId: string;
  taskText: string;
  taskHints?: string;
  startedAt: string;
  endedAt?: string;
  visits: PageVisit[];
  summary?: SessionSummary;
  currentVisit?: InProgressVisit;
  /** When set, session is paused; no classification, overlay hidden, elapsed frozen. */
  pausedAt?: string;
  /** Cumulative seconds the session has been paused (supports multiple pause/resume cycles). */
  totalPausedSeconds?: number;
  /** When paused: who triggered the pause; only "idle" triggers auto-resume when user becomes active. */
  pausedBy?: "user" | "idle";
}

export interface InProgressVisit {
  visitToken: string;
  tabId: number;
  url: string;
  domain: string;
  title: string;
  enteredAt: string;
  visitState: "classifying" | "classified" | "error";
  classification?: Classification;
  confidence?: number;
  reason?: string;
  score?: number;
  /** Set when classification was reused from previous visit (same tab, same domain); no alert should be sent. */
  reusedClassification?: true;
}

export interface DomainAggregate {
  domain: string;
  totalSeconds: number;
  visitCount: number;
}

export interface DailyStats {
  date: string;
  totalSessions: number;
  totalTrackedSeconds: number;
  totalAlignedSeconds: number;
  totalDistractingSeconds: number;
  distractionCount: number;
  averageContextSwitchCostSeconds: number;
  focusPercentage: number;
  mostCommonDistractingDomains: DomainAggregate[];
  mostCommonAlignedDomains: DomainAggregate[];
}

export type DateRange = "today" | "7d" | "30d" | "all";

export interface AnalyticsInsights {
  bestFocusDay: { date: string; focusPercentage: number } | null;
  averageSessionLengthSeconds: number;
}

export interface AnalyticsResponse {
  range: DateRange;
  from: string;
  to: string;
  statsByDay: DailyStats[];
  totals: Omit<DailyStats, "date">;
  recentSessions: CompletedSession[];
  sessionsByDay: Record<string, CompletedSession[]>;
  insights: AnalyticsInsights;
}

export interface RuntimeState {
  settings: Settings;
  activeSession: FocusSession | null;
  lastSummary: SessionSummary | null;
  lastError: string | null;
  /** True when a valid Supabase access token is present in storage. */
  isAuthenticated: boolean;
}

export type RuntimeRequest =
  | { type: "GET_STATE" }
  | { type: "START_SESSION"; taskText: string; taskHints?: string }
  | { type: "END_SESSION" }
  | { type: "PAUSE_SESSION" }
  | { type: "RESUME_SESSION" }
  | { type: "GET_ANALYTICS"; range: DateRange }
  | { type: "UPDATE_SETTINGS"; payload: Partial<Settings> }
  | { type: "CLEAR_ERROR" }
  | { type: "OPEN_OPTIONS" }
  | { type: "OPEN_SIDE_PANEL" }
  | { type: "GET_INTEGRATION_STATE" }
  | { type: "SIGN_OUT" }
  | { type: "REFRESH_PLAN" };

export type RuntimeResponse<T = unknown> = {
  ok: boolean;
  error?: string;
  data?: T;
};

export type IconState = "default" | "aligned" | "neutral" | "distracting" | "analyzing" | "error";

const createSvgDataUrl = (bg: string, symbol: string): string => {
  const svg = `<svg width="128" height="128" viewBox="0 0 128 128" xmlns="http://www.w3.org/2000/svg">
    <rect width="128" height="128" rx="20" fill="${bg}"/>
    <text x="64" y="72" font-size="72" font-weight="bold" fill="white" text-anchor="middle" font-family="Arial">${symbol}</text>
  </svg>`;
  return `data:image/svg+xml,${encodeURIComponent(svg)}`;
};

export const ICON_DATA_URLS: Record<IconState, string> = {
  default: createSvgDataUrl("#6366f1", ""),
  aligned: createSvgDataUrl("#10b981", "✓"),
  neutral: createSvgDataUrl("#f59e0b", "•"),
  distracting: createSvgDataUrl("#ef4444", "✕"),
  analyzing: createSvgDataUrl("#a855f7", "…"),
  error: createSvgDataUrl("#a855f7", "!")
};

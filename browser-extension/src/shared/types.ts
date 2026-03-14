export type Classification = "aligned" | "distracting";

export interface ClassificationResult {
  classification: Classification;
  confidence: number;
  reason?: string;
}

export interface Settings {
  openAiApiKey: string;
  classifierModel: string;
  onboardingCompleted: boolean;
  excludedDomains: string[];
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

export interface FocusSession {
  sessionId: string;
  taskText: string;
  startedAt: string;
  endedAt?: string;
  visits: PageVisit[];
  summary?: SessionSummary;
  currentVisit?: InProgressVisit;
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

export type DateRange = "today" | "7d" | "30d";

export interface AnalyticsResponse {
  range: DateRange;
  from: string;
  to: string;
  statsByDay: DailyStats[];
  totals: Omit<DailyStats, "date">;
  recentSessions: FocusSession[];
}

export interface RuntimeState {
  settings: Settings;
  activeSession: FocusSession | null;
  lastSummary: SessionSummary | null;
  lastError: string | null;
}

export type RuntimeRequest =
  | { type: "GET_STATE" }
  | { type: "START_SESSION"; taskText: string }
  | { type: "END_SESSION" }
  | { type: "GET_ANALYTICS"; range: DateRange }
  | { type: "UPDATE_SETTINGS"; payload: Partial<Settings> }
  | { type: "CLEAR_ERROR" }
  | { type: "OPEN_OPTIONS" }
  | { type: "OPEN_ANALYTICS" }
  | { type: "OPEN_SIDE_PANEL" };

export type RuntimeResponse<T = unknown> = {
  ok: boolean;
  error?: string;
  data?: T;
};

export type IconState = "default" | "aligned" | "distracting" | "analyzing" | "error";

const createSvgDataUrl = (bg: string, symbol: string): string => {
  const svg = `<svg width="128" height="128" viewBox="0 0 128 128" xmlns="http://www.w3.org/2000/svg">
    <rect width="128" height="128" rx="20" fill="${bg}"/>
    <text x="64" y="72" font-size="72" font-weight="bold" fill="white" text-anchor="middle" font-family="Arial">${symbol}</text>
  </svg>`;
  return `data:image/svg+xml;base64,${btoa(svg)}`;
};

export const ICON_DATA_URLS: Record<IconState, string> = {
  default: createSvgDataUrl("#6366f1", ""),
  aligned: createSvgDataUrl("#10b981", "✓"),
  distracting: createSvgDataUrl("#ef4444", "✕"),
  analyzing: createSvgDataUrl("#a855f7", "…"),
  error: createSvgDataUrl("#a855f7", "!")
};

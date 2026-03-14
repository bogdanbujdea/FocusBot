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

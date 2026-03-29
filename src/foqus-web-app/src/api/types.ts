export interface MeResponse {
  userId: string;
  email: string;
  subscriptionStatus: string;
  planType: number;
}

export interface SubscriptionStatusResponse {
  status: string;
  planType: number;
  trialEndsAt?: string;
  currentPeriodEndsAt?: string;
  nextBilledAtUtc?: string;
}

export interface PricingPlanDto {
  priceId: string;
  name: string;
  description?: string;
  unitAmountMinor: number;
  currency: string;
  billingInterval?: string;
  planType: string;
}

export interface PricingResponse {
  plans: PricingPlanDto[];
  clientToken: string;
  isSandbox: boolean;
}

export interface CustomerPortalResponse {
  url: string;
}

export type ApiMutationResult<T> =
  | { ok: true; data: T }
  | { ok: false; status: number; error?: string };

export interface StartSessionRequest {
  sessionTitle: string;
  sessionContext?: string;
  clientId?: string;
}

export interface EndSessionRequest {
  focusScorePercent: number;
  focusedSeconds: number;
  distractedSeconds: number;
  distractionCount: number;
  contextSwitchCount: number;
  clientId?: string;
}

export interface SessionResponse {
  id: string;
  sessionTitle: string;
  sessionContext?: string;
  clientId?: string;
  startedAtUtc: string;
  endedAtUtc?: string;
  pausedAtUtc?: string | null;
  totalPausedSeconds: number;
  isPaused: boolean;
  focusScorePercent?: number;
  focusedSeconds?: number;
  distractedSeconds?: number;
  distractionCount?: number;
  contextSwitchCount?: number;
  source: string;
}

export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AnalyticsSummaryResponse {
  period: { from: string; to: string };
  totalSessions: number;
  totalFocusedSeconds: number;
  totalDistractedSeconds: number;
  averageFocusScorePercent: number;
  totalDistractionCount: number;
  totalContextSwitchCount: number;
  averageSessionDurationSeconds: number;
  longestSessionSeconds: number;
  clientsActive: number;
  totalActiveSeconds: number;
}

export interface TrendDataPoint {
  date: string;
  sessions: number;
  focusedSeconds: number;
  distractedSeconds: number;
  focusScorePercent: number;
  distractionCount: number;
}

export interface AnalyticsTrendsResponse {
  granularity: string;
  dataPoints: TrendDataPoint[];
}

export interface ClientAnalytics {
  clientId: string;
  clientType: string;
  name: string;
  sessions: number;
  focusedSeconds: number;
  distractedSeconds: number;
  focusScorePercent: number;
}

export interface AnalyticsClientsResponse {
  clients: ClientAnalytics[];
}

export const PlanType = {
  TrialFullAccess: 0,
  CloudBYOK: 1,
  CloudManaged: 2,
} as const;

export type PlanTypeValue = (typeof PlanType)[keyof typeof PlanType];

export function getPlanDisplayName(planType: number): string {
  switch (planType) {
    case PlanType.CloudBYOK:
      return "Foqus BYOK";
    case PlanType.CloudManaged:
      return "Foqus Premium";
    case PlanType.TrialFullAccess:
      return "Trial";
    default:
      return "No active plan";
  }
}

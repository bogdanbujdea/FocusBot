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
}

export type ApiMutationResult<T> =
  | { ok: true; data: T }
  | { ok: false; status: number; error?: string };

export interface StartSessionRequest {
  sessionTitle: string;
  sessionContext?: string;
  deviceId?: string;
}

export interface EndSessionRequest {
  focusScorePercent: number;
  focusedSeconds: number;
  distractedSeconds: number;
  distractionCount: number;
  contextSwitchCount: number;
  deviceId?: string;
}

export interface SessionResponse {
  id: string;
  sessionTitle: string;
  sessionContext?: string;
  deviceId?: string;
  startedAtUtc: string;
  endedAtUtc?: string;
  pausedAtUtc?: string;
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
  devicesActive: number;
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

export interface DeviceAnalytics {
  deviceId: string;
  deviceType: string;
  name: string;
  sessions: number;
  focusedSeconds: number;
  distractedSeconds: number;
  focusScorePercent: number;
}

export interface AnalyticsDevicesResponse {
  devices: DeviceAnalytics[];
}

export const PlanType = {
  FreeBYOK: 0,
  CloudBYOK: 1,
  CloudManaged: 2,
} as const;

export type PlanTypeValue = (typeof PlanType)[keyof typeof PlanType];

export function getPlanDisplayName(planType: number): string {
  switch (planType) {
    case PlanType.FreeBYOK:
      return "Free (BYOK)";
    case PlanType.CloudBYOK:
      return "Cloud BYOK";
    case PlanType.CloudManaged:
      return "Cloud Managed";
    default:
      return "Unknown";
  }
}

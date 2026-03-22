import type { SessionResponse, TrendDataPoint } from "../api/types";

export type FocusScoreTone = "high" | "mid" | "low";

/**
 * Maps focus score percentage to a tone for table/chart styling.
 * high: >= 70, mid: 40–69, low: < 40
 */
export function focusScoreTone(
  percent: number | null | undefined
): FocusScoreTone | null {
  if (percent === null || percent === undefined) return null;
  if (percent >= 70) return "high";
  if (percent >= 40) return "mid";
  return "low";
}

export function focusScoreToneClass(
  percent: number | null | undefined
): string {
  const tone = focusScoreTone(percent);
  if (!tone) return "";
  return `focus-pct-${tone}`;
}

/**
 * Active wall-clock seconds for an ended session: (ended - started) - totalPausedSeconds.
 */
export function computeEndedSessionActiveSeconds(
  session: Pick<
    SessionResponse,
    "startedAtUtc" | "endedAtUtc" | "totalPausedSeconds"
  >
): number {
  if (!session.endedAtUtc) return 0;
  const start = new Date(session.startedAtUtc).getTime();
  const end = new Date(session.endedAtUtc).getTime();
  const wall = Math.floor((end - start) / 1000);
  return Math.max(0, wall - session.totalPausedSeconds);
}

/**
 * Same as AnalyticsPage trend mapping: seconds to minutes for charts.
 */
export function secondsToChartMinutes(seconds: number): number {
  return Math.round(seconds / 60);
}

/**
 * Average seconds per distraction episode when count > 0.
 */
export function averageDistractionDurationSeconds(
  totalDistractedSeconds: number,
  distractionCount: number
): number | null {
  if (distractionCount <= 0) return null;
  return totalDistractedSeconds / distractionCount;
}

export function averageDistractionsPerSession(
  totalDistractionCount: number,
  totalSessions: number
): number | null {
  if (totalSessions <= 0) return null;
  return totalDistractionCount / totalSessions;
}

/**
 * Elapsed active seconds for a live or paused session (not ended).
 */
/** Maps API trend points to Recharts rows (minutes rounded like AnalyticsPage). */
export function mapTrendDataPointForChart(dp: TrendDataPoint) {
  return {
    ...dp,
    focusedMinutes: secondsToChartMinutes(dp.focusedSeconds),
    distractedMinutes: secondsToChartMinutes(dp.distractedSeconds),
  };
}

export function computeLiveSessionActiveSeconds(
  startedAtUtc: string,
  pausedAtUtc: string | undefined,
  totalPausedSeconds: number,
  nowMs: number = Date.now()
): number {
  const start = new Date(startedAtUtc).getTime();
  const endRef =
    pausedAtUtc !== undefined && pausedAtUtc !== ""
      ? new Date(pausedAtUtc).getTime()
      : nowMs;
  const wall = Math.floor((endRef - start) / 1000);
  return Math.max(0, wall - totalPausedSeconds);
}

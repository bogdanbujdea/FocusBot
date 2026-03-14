import { useEffect, useMemo, useState } from "react";
import { sendRuntimeRequest } from "../shared/runtime";
import { calculateLiveSummary } from "../shared/metrics";
import type { AnalyticsResponse, RuntimeState, SessionSummary } from "../shared/types";
import { formatSeconds } from "../shared/utils";

interface SummaryCardProps {
  state: RuntimeState;
}

const MiniFocusGauge = ({ percentage }: { percentage: number }): JSX.Element => {
  const size = 60;
  const radius = 24;
  const circumference = 2 * Math.PI * radius;
  const strokeDashoffset = circumference - (percentage / 100) * circumference;

  return (
    <div className="mini-focus-gauge">
      <svg viewBox={`0 0 ${size} ${size}`}>
        <circle className="mini-focus-gauge-bg" cx={size / 2} cy={size / 2} r={radius} />
        <circle
          className="mini-focus-gauge-fill"
          cx={size / 2}
          cy={size / 2}
          r={radius}
          strokeDasharray={circumference}
          strokeDashoffset={strokeDashoffset}
        />
      </svg>
      <div className="mini-focus-gauge-text">
        <span className="mini-focus-gauge-value">{percentage.toFixed(0)}%</span>
      </div>
    </div>
  );
};

interface MetricsSummary {
  focusPercentage: number;
  distractionCount: number;
  contextSwitchCostSeconds: number;
}

const SummaryBlock = ({ title, summary }: { title: string; summary: MetricsSummary }): JSX.Element => (
  <>
    {title ? <h2>{title}</h2> : null}
    <div className="popup-summary">
      <MiniFocusGauge percentage={summary.focusPercentage} />
      <div className="popup-summary-metrics">
        <p className="popup-metric">
          <span className="popup-metric-label">Focus</span>
          <span className="popup-metric-value">{summary.focusPercentage.toFixed(0)}%</span>
        </p>
        <p className="popup-metric">
          <span className="popup-metric-label">Distractions</span>
          <span className="popup-metric-value">{summary.distractionCount}</span>
        </p>
        <p className="popup-metric">
          <span className="popup-metric-label">Avg switch cost</span>
          <span className="popup-metric-value">{formatSeconds(summary.contextSwitchCostSeconds)}</span>
        </p>
      </div>
    </div>
  </>
);

const sessionSummaryToMetrics = (s: SessionSummary): MetricsSummary => ({
  focusPercentage: s.focusPercentage,
  distractionCount: s.distractionCount,
  contextSwitchCostSeconds: s.contextSwitchCostSeconds
});

const todayTotalsToMetrics = (totals: AnalyticsResponse["totals"]): MetricsSummary => ({
  focusPercentage: totals.focusPercentage,
  distractionCount: totals.distractionCount,
  contextSwitchCostSeconds: totals.averageContextSwitchCostSeconds
});

export const SummaryCard = ({ state }: SummaryCardProps): JSX.Element => {
  const [tick, setTick] = useState(0);
  const [todayAnalytics, setTodayAnalytics] = useState<AnalyticsResponse | null>(null);

  useEffect(() => {
    if (!state.activeSession) return;
    const id = setInterval(() => setTick((t) => t + 1), 1000);
    return () => clearInterval(id);
  }, [state.activeSession]);

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      const res = await sendRuntimeRequest<AnalyticsResponse>({ type: "GET_ANALYTICS", range: "today" });
      if (cancelled || !res.ok || !res.data) return;
      setTodayAnalytics(res.data);
    })();
    return () => {
      cancelled = true;
    };
  }, [state.activeSession]);

  const liveSummary = useMemo(() => {
    if (!state.activeSession) return null;
    return calculateLiveSummary(state.activeSession);
  }, [state.activeSession, tick]);

  const hasActiveSession = Boolean(state.activeSession && liveSummary);
  const todayHasData = todayAnalytics && todayAnalytics.totals.totalSessions > 0;

  return (
    <section className="card">
      {hasActiveSession ? (
        <>
          <h2>Current task</h2>
          <p className="popup-task-text">{state.activeSession!.taskText}</p>
          <SummaryBlock title="Current task analytics" summary={sessionSummaryToMetrics(liveSummary!)} />
        </>
      ) : null}

      <h2>Today&apos;s analytics</h2>
      {todayHasData ? (
        <SummaryBlock title="" summary={todayTotalsToMetrics(todayAnalytics!.totals)} />
      ) : (
        <p className="muted">No focus sessions today. Start one to see analytics.</p>
      )}
    </section>
  );
};

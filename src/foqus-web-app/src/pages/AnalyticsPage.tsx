import { useEffect, useState } from "react";
import {
  AreaChart,
  Area,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  BarChart,
  Bar,
} from "recharts";
import { api } from "../api/client";
import type {
  AnalyticsSummaryResponse,
  AnalyticsTrendsResponse,
  AnalyticsDevicesResponse,
  SessionResponse,
  PaginatedResponse,
} from "../api/types";
import { KpiCard } from "../components/KpiCard";
import { SegmentedControl } from "../components/SegmentedControl";
import {
  averageDistractionDurationSeconds,
  averageDistractionsPerSession,
  computeEndedSessionActiveSeconds,
  focusScoreToneClass,
  mapTrendDataPointForChart,
} from "../utils/analyticsDisplay";
import { formatDateTime, formatDuration, daysAgo } from "../utils/format";
import "./AnalyticsPage.css";

type DatePreset = "7d" | "30d" | "90d";

const PRESET_OPTIONS = [
  { value: "7d" as const, label: "7 Days" },
  { value: "30d" as const, label: "30 Days" },
  { value: "90d" as const, label: "90 Days" },
];

export function AnalyticsPage() {
  const [preset, setPreset] = useState<DatePreset>("7d");
  const [summary, setSummary] = useState<AnalyticsSummaryResponse | null>(
    null
  );
  const [trends, setTrends] = useState<AnalyticsTrendsResponse | null>(null);
  const [devices, setDevices] = useState<AnalyticsDevicesResponse | null>(
    null
  );
  const [sessions, setSessions] =
    useState<PaginatedResponse<SessionResponse> | null>(null);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);

  const daysForPreset = (p: DatePreset) =>
    p === "7d" ? 7 : p === "30d" ? 30 : 90;

  const granularityForPreset = (p: DatePreset) =>
    p === "7d" ? "daily" : p === "30d" ? "daily" : "weekly";

  useEffect(() => {
    let cancelled = false;
    async function load() {
      setLoading(true);
      const days = daysForPreset(preset);
      const from = daysAgo(days);
      const to = new Date().toISOString();

      const [s, t, d, sess] = await Promise.all([
        api.getAnalyticsSummary({ from, to }),
        api.getAnalyticsTrends({
          from,
          to,
          granularity: granularityForPreset(preset),
        }),
        api.getAnalyticsDevices({ from, to }),
        api.getSessions({ page, pageSize: 10, from, to }),
      ]);

      if (!cancelled) {
        setSummary(s);
        setTrends(t);
        setDevices(d);
        setSessions(sess);
        setLoading(false);
      }
    }
    void load();
    return () => {
      cancelled = true;
    };
  }, [preset, page]);

  const trendData = (trends?.dataPoints ?? []).map(mapTrendDataPointForChart);

  const deviceData = (devices?.devices ?? []).map((d) => ({
    ...d,
    focusedMinutes: Math.round(d.focusedSeconds / 60),
    distractedMinutes: Math.round(d.distractedSeconds / 60),
  }));

  const s = summary;
  const hasSessions = (s?.totalSessions ?? 0) > 0;
  const focusPct = s?.averageFocusScorePercent ?? 0;
  const trackedSeconds =
    (s?.totalFocusedSeconds ?? 0) + (s?.totalDistractedSeconds ?? 0);
  const avgDistractionSec = averageDistractionDurationSeconds(
    s?.totalDistractedSeconds ?? 0,
    s?.totalDistractionCount ?? 0
  );
  const avgDistPerSess = averageDistractionsPerSession(
    s?.totalDistractionCount ?? 0,
    s?.totalSessions ?? 0
  );

  return (
    <div className="analytics-page">
      <header className="page-header">
        <div className="page-header-row">
          <div>
            <h1 className="page-title">Focus Insights</h1>
            <p className="page-subtitle">
              Historic focus trends and session history
            </p>
          </div>
          <SegmentedControl
            options={PRESET_OPTIONS}
            value={preset}
            onChange={(p) => {
              setPage(1);
              setPreset(p);
            }}
            ariaLabel="Date range"
          />
        </div>
      </header>

      {loading ? (
        <div className="analytics-loading">Loading analytics...</div>
      ) : (
        <>
          <div className="analytics-kpi-grid">
            <KpiCard
              variant="focus-score"
              label="Focus score"
              value={hasSessions ? undefined : "—"}
              sublabel={
                hasSessions
                  ? `Average across ${s?.totalSessions ?? 0} session${
                      (s?.totalSessions ?? 0) === 1 ? "" : "s"
                    } in this period`
                  : "No sessions in this range"
              }
              focusPercentage={hasSessions ? focusPct : 0}
              focusDetails={
                <div className="kpi-focus-detail-row">
                  <span className="kpi-focus-detail-aligned">
                    Deep work {formatDuration(s?.totalFocusedSeconds ?? 0)}
                  </span>
                  <span className="kpi-focus-detail-distracting">
                    Distracting {formatDuration(s?.totalDistractedSeconds ?? 0)}
                  </span>
                </div>
              }
            />
            <KpiCard
              variant="aligned"
              label="Deep work"
              value={formatDuration(s?.totalFocusedSeconds ?? 0)}
              sublabel="Total aligned time"
            />
            <KpiCard
              label="Sessions"
              value={s?.totalSessions ?? 0}
              sublabel={`Tracked ${formatDuration(trackedSeconds)} total`}
            />
            <KpiCard
              variant="distracted"
              label="Distractions"
              value={s?.totalDistractionCount ?? 0}
              sublabel={
                avgDistPerSess !== null
                  ? `Avg ${avgDistPerSess.toFixed(1)} per session`
                  : "No distraction data"
              }
            />
            <KpiCard
              label="Avg session"
              value={formatDuration(s?.averageSessionDurationSeconds ?? 0)}
              sublabel={
                avgDistractionSec !== null
                  ? `Avg distraction block ${formatDuration(
                      Math.round(avgDistractionSec)
                    )}`
                  : "Length of completed sessions"
              }
            />
            <KpiCard
              label="Longest session"
              value={formatDuration(s?.longestSessionSeconds ?? 0)}
              sublabel="Best single block in period"
            />
          </div>

          {trendData.length > 0 && (
            <section className="chart-section">
              <h2 className="section-title">Focus trend</h2>
              <div className="chart-container">
                <ResponsiveContainer width="100%" height={300}>
                  <AreaChart data={trendData}>
                    <CartesianGrid
                      strokeDasharray="3 3"
                      stroke="rgba(255,255,255,0.06)"
                    />
                    <XAxis
                      dataKey="date"
                      stroke="rgba(231,236,255,0.5)"
                      fontSize={12}
                    />
                    <YAxis
                      stroke="rgba(231,236,255,0.5)"
                      fontSize={12}
                    />
                    <Tooltip
                      contentStyle={{
                        backgroundColor: "rgba(15, 18, 32, 0.95)",
                        border: "1px solid rgba(255,255,255,0.12)",
                        borderRadius: "8px",
                        color: "#e7ecff",
                      }}
                    />
                    <Area
                      type="monotone"
                      dataKey="focusedMinutes"
                      name="Focused (min)"
                      stroke="#2db871"
                      fill="rgba(45, 184, 113, 0.2)"
                    />
                    <Area
                      type="monotone"
                      dataKey="distractedMinutes"
                      name="Distracted (min)"
                      stroke="#ff6666"
                      fill="rgba(255, 102, 102, 0.2)"
                    />
                  </AreaChart>
                </ResponsiveContainer>
              </div>
            </section>
          )}

          {deviceData.length > 0 && (
            <section className="chart-section">
              <h2 className="section-title">Device breakdown</h2>
              <div className="chart-container">
                <ResponsiveContainer width="100%" height={250}>
                  <BarChart data={deviceData}>
                    <CartesianGrid
                      strokeDasharray="3 3"
                      stroke="rgba(255,255,255,0.06)"
                    />
                    <XAxis
                      dataKey="name"
                      stroke="rgba(231,236,255,0.5)"
                      fontSize={12}
                    />
                    <YAxis
                      stroke="rgba(231,236,255,0.5)"
                      fontSize={12}
                    />
                    <Tooltip
                      contentStyle={{
                        backgroundColor: "rgba(15, 18, 32, 0.95)",
                        border: "1px solid rgba(255,255,255,0.12)",
                        borderRadius: "8px",
                        color: "#e7ecff",
                      }}
                    />
                    <Bar
                      dataKey="sessions"
                      name="Sessions"
                      fill="#365cff"
                      radius={[4, 4, 0, 0]}
                    />
                  </BarChart>
                </ResponsiveContainer>
              </div>
            </section>
          )}

          <section className="chart-section">
            <h2 className="section-title">Session history</h2>
            {!sessions?.items.length ? (
              <div className="empty-state">
                No sessions found for this period.
              </div>
            ) : (
              <>
                <table className="sessions-table">
                  <thead>
                    <tr>
                      <th>Session</th>
                      <th>Date</th>
                      <th>Duration</th>
                      <th>Focus</th>
                      <th>Distractions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {sessions.items.map((sess) => {
                      const duration = computeEndedSessionActiveSeconds(sess);
                      const fp = sess.focusScorePercent;
                      return (
                        <tr key={sess.id}>
                          <td>{sess.sessionTitle}</td>
                          <td>{formatDateTime(sess.startedAtUtc)}</td>
                          <td>{formatDuration(duration)}</td>
                          <td
                            className={focusScoreToneClass(fp)}
                          >
                            {fp !== undefined && fp !== null ? `${fp}%` : "—"}
                          </td>
                          <td>{sess.distractionCount ?? 0}</td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
                <div className="pagination">
                  <button
                    type="button"
                    disabled={page <= 1}
                    onClick={() => setPage((p) => p - 1)}
                    className="pagination-btn"
                  >
                    Previous
                  </button>
                  <span className="pagination-info">
                    Page {sessions.page} of{" "}
                    {Math.ceil(sessions.totalCount / sessions.pageSize)}
                  </span>
                  <button
                    type="button"
                    disabled={
                      page >=
                      Math.ceil(sessions.totalCount / sessions.pageSize)
                    }
                    onClick={() => setPage((p) => p + 1)}
                    className="pagination-btn"
                  >
                    Next
                  </button>
                </div>
              </>
            )}
          </section>
        </>
      )}
    </div>
  );
}

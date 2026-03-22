import { useEffect, useState, useCallback } from "react";
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
import { formatDuration, formatDateTime, daysAgo } from "../utils/format";
import "./AnalyticsPage.css";

type DatePreset = "7d" | "30d" | "90d";

export function AnalyticsPage() {
  const [preset, setPreset] = useState<DatePreset>("7d");
  const [summary, setSummary] = useState<AnalyticsSummaryResponse | null>(null);
  const [trends, setTrends] = useState<AnalyticsTrendsResponse | null>(null);
  const [devices, setDevices] = useState<AnalyticsDevicesResponse | null>(null);
  const [sessions, setSessions] = useState<PaginatedResponse<SessionResponse> | null>(null);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);

  const daysForPreset = (p: DatePreset) =>
    p === "7d" ? 7 : p === "30d" ? 30 : 90;

  const granularityForPreset = (p: DatePreset) =>
    p === "7d" ? "daily" : p === "30d" ? "daily" : "weekly";

  const loadData = useCallback(async () => {
    setLoading(true);
    const days = daysForPreset(preset);
    const from = daysAgo(days);
    const to = new Date().toISOString();

    const [s, t, d, sess] = await Promise.all([
      api.getAnalyticsSummary({ from, to }),
      api.getAnalyticsTrends({ from, to, granularity: granularityForPreset(preset) }),
      api.getAnalyticsDevices({ from, to }),
      api.getSessions({ page, pageSize: 10, from, to }),
    ]);

    setSummary(s);
    setTrends(t);
    setDevices(d);
    setSessions(sess);
    setLoading(false);
  }, [preset, page]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const trendData = (trends?.dataPoints ?? []).map((dp) => ({
    ...dp,
    focusedMinutes: Math.round(dp.focusedSeconds / 60),
    distractedMinutes: Math.round(dp.distractedSeconds / 60),
  }));

  const deviceData = (devices?.devices ?? []).map((d) => ({
    ...d,
    focusedMinutes: Math.round(d.focusedSeconds / 60),
    distractedMinutes: Math.round(d.distractedSeconds / 60),
  }));

  return (
    <div className="analytics-page">
      <header className="page-header">
        <div className="page-header-row">
          <div>
            <h1 className="page-title">Analytics</h1>
            <p className="page-subtitle">Detailed focus trends and insights</p>
          </div>
          <div className="date-presets">
            {(["7d", "30d", "90d"] as DatePreset[]).map((p) => (
              <button
                key={p}
                className={`preset-btn ${preset === p ? "active" : ""}`}
                onClick={() => {
                  setPage(1);
                  setPreset(p);
                }}
              >
                {p === "7d" ? "7 Days" : p === "30d" ? "30 Days" : "90 Days"}
              </button>
            ))}
          </div>
        </div>
      </header>

      {loading ? (
        <div className="analytics-loading">Loading analytics...</div>
      ) : (
        <>
          <div className="summary-grid">
            <SummaryCard label="Total Sessions" value={summary?.totalSessions ?? 0} />
            <SummaryCard
              label="Avg Focus Score"
              value={`${summary?.averageFocusScorePercent ?? 0}%`}
            />
            <SummaryCard
              label="Focus Time"
              value={formatDuration(summary?.totalFocusedSeconds ?? 0)}
            />
            <SummaryCard
              label="Distraction Time"
              value={formatDuration(summary?.totalDistractedSeconds ?? 0)}
            />
            <SummaryCard
              label="Distractions"
              value={summary?.totalDistractionCount ?? 0}
            />
            <SummaryCard
              label="Avg Session"
              value={formatDuration(summary?.averageSessionDurationSeconds ?? 0)}
            />
          </div>

          {trendData.length > 0 && (
            <section className="chart-section">
              <h2 className="section-title">Focus Trend</h2>
              <div className="chart-container">
                <ResponsiveContainer width="100%" height={300}>
                  <AreaChart data={trendData}>
                    <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.06)" />
                    <XAxis dataKey="date" stroke="rgba(255,255,255,0.4)" fontSize={12} />
                    <YAxis stroke="rgba(255,255,255,0.4)" fontSize={12} />
                    <Tooltip
                      contentStyle={{
                        backgroundColor: "#2a2242",
                        border: "1px solid rgba(255,255,255,0.12)",
                        borderRadius: "8px",
                        color: "#fff",
                      }}
                    />
                    <Area
                      type="monotone"
                      dataKey="focusedMinutes"
                      name="Focused (min)"
                      stroke="#22c55e"
                      fill="rgba(34,197,94,0.2)"
                    />
                    <Area
                      type="monotone"
                      dataKey="distractedMinutes"
                      name="Distracted (min)"
                      stroke="#ef4444"
                      fill="rgba(239,68,68,0.2)"
                    />
                  </AreaChart>
                </ResponsiveContainer>
              </div>
            </section>
          )}

          {deviceData.length > 0 && (
            <section className="chart-section">
              <h2 className="section-title">Device Breakdown</h2>
              <div className="chart-container">
                <ResponsiveContainer width="100%" height={250}>
                  <BarChart data={deviceData}>
                    <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.06)" />
                    <XAxis dataKey="name" stroke="rgba(255,255,255,0.4)" fontSize={12} />
                    <YAxis stroke="rgba(255,255,255,0.4)" fontSize={12} />
                    <Tooltip
                      contentStyle={{
                        backgroundColor: "#2a2242",
                        border: "1px solid rgba(255,255,255,0.12)",
                        borderRadius: "8px",
                        color: "#fff",
                      }}
                    />
                    <Bar dataKey="sessions" name="Sessions" fill="#8b5cf6" radius={[4, 4, 0, 0]} />
                  </BarChart>
                </ResponsiveContainer>
              </div>
            </section>
          )}

          <section className="chart-section">
            <h2 className="section-title">Session History</h2>
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
                      <th>Focus Score</th>
                      <th>Distractions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {sessions.items.map((s) => {
                      const duration = s.endedAtUtc
                        ? Math.floor(
                            (new Date(s.endedAtUtc).getTime() -
                              new Date(s.startedAtUtc).getTime()) /
                              1000
                          ) - s.totalPausedSeconds
                        : 0;

                      return (
                        <tr key={s.id}>
                          <td>{s.sessionTitle}</td>
                          <td>{formatDateTime(s.startedAtUtc)}</td>
                          <td>{formatDuration(duration)}</td>
                          <td>
                            {s.focusScorePercent !== null &&
                            s.focusScorePercent !== undefined
                              ? `${s.focusScorePercent}%`
                              : "--"}
                          </td>
                          <td>{s.distractionCount ?? 0}</td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
                <div className="pagination">
                  <button
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

function SummaryCard({
  label,
  value,
}: {
  label: string;
  value: string | number;
}) {
  return (
    <div className="stat-card">
      <div className="stat-label">{label}</div>
      <div className="stat-value">{value}</div>
    </div>
  );
}

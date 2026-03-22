import { useEffect, useState } from "react";
import { api } from "../api/client";
import type { SessionResponse, AnalyticsSummaryResponse } from "../api/types";
import { formatDuration, formatDateTime, daysAgo } from "../utils/format";
import "./DashboardPage.css";

export function DashboardPage() {
  const [summary, setSummary] = useState<AnalyticsSummaryResponse | null>(null);
  const [activeSession, setActiveSession] = useState<SessionResponse | null>(
    null
  );
  const [recentSessions, setRecentSessions] = useState<SessionResponse[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    async function load() {
      const today = new Date();
      today.setHours(0, 0, 0, 0);

      const [summaryData, active, sessions] = await Promise.all([
        api.getAnalyticsSummary({ from: daysAgo(1), to: new Date().toISOString() }),
        api.getActiveSession(),
        api.getSessions({ page: 1, pageSize: 5 }),
      ]);

      setSummary(summaryData);
      setActiveSession(active);
      setRecentSessions(sessions?.items ?? []);
      setLoading(false);
    }
    load();
  }, []);

  if (loading) {
    return (
      <div className="dashboard-page">
        <header className="page-header">
          <h1 className="page-title">Dashboard</h1>
          <p className="page-subtitle">Loading your focus data...</p>
        </header>
      </div>
    );
  }

  const focusScore = summary?.averageFocusScorePercent ?? 0;

  return (
    <div className="dashboard-page">
      <header className="page-header">
        <h1 className="page-title">Dashboard</h1>
        <p className="page-subtitle">Your focus overview across all devices</p>
      </header>

      {activeSession && (
        <section className="active-session-banner">
          <div className="active-session-indicator" />
          <div className="active-session-info">
            <strong>{activeSession.sessionTitle}</strong>
            <span className="active-session-meta">
              Started {formatDateTime(activeSession.startedAtUtc)}
              {activeSession.isPaused && " (paused)"}
            </span>
          </div>
        </section>
      )}

      <div className="dashboard-grid">
        <div className="stat-card">
          <div className="stat-label">Today's Focus</div>
          <div className="stat-value">
            {summary?.totalSessions ? `${focusScore}%` : "--"}
          </div>
          {!summary?.totalSessions && (
            <div className="stat-hint">
              Complete a session to see your score
            </div>
          )}
        </div>

        <div className="stat-card">
          <div className="stat-label">Sessions Today</div>
          <div className="stat-value">{summary?.totalSessions ?? 0}</div>
        </div>

        <div className="stat-card">
          <div className="stat-label">Focus Time</div>
          <div className="stat-value">
            {formatDuration(summary?.totalFocusedSeconds ?? 0)}
          </div>
        </div>

        <div className="stat-card">
          <div className="stat-label">Distraction Time</div>
          <div className="stat-value">
            {formatDuration(summary?.totalDistractedSeconds ?? 0)}
          </div>
        </div>
      </div>

      <section className="recent-sessions">
        <h2 className="section-title">Recent Sessions</h2>
        {recentSessions.length === 0 ? (
          <div className="empty-state">
            <p>
              No sessions yet. Start a focus session in the desktop app or
              browser extension.
            </p>
          </div>
        ) : (
          <div className="sessions-list">
            {recentSessions.map((s) => (
              <div key={s.id} className="session-row">
                <div className="session-title">{s.sessionTitle}</div>
                <div className="session-meta">
                  <span>{formatDateTime(s.startedAtUtc)}</span>
                  <span>
                    {s.focusScorePercent !== null &&
                    s.focusScorePercent !== undefined
                      ? `${s.focusScorePercent}%`
                      : "--"}
                  </span>
                  <span>
                    {formatDuration(
                      s.endedAtUtc
                        ? Math.floor(
                            (new Date(s.endedAtUtc).getTime() -
                              new Date(s.startedAtUtc).getTime()) /
                              1000
                          ) - s.totalPausedSeconds
                        : 0
                    )}
                  </span>
                </div>
              </div>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}

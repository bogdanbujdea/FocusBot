import { useCallback, useEffect, useState } from "react";
import { api } from "../api/client";
import { connectFocusHub, disconnectFocusHub } from "../api/signalr";
import type { AnalyticsSummaryResponse, SessionResponse } from "../api/types";
import { KpiCard } from "../components/KpiCard";
import { SessionTimer } from "../components/SessionTimer";
import {
  averageDistractionDurationSeconds,
  computeEndedSessionActiveSeconds,
  computeLiveSessionActiveSeconds,
} from "../utils/analyticsDisplay";
import {
  formatDateTime,
  formatDuration,
  startOfLocalDayIso,
} from "../utils/format";
import "./DashboardPage.css";

export function DashboardPage() {
  const [summary, setSummary] = useState<AnalyticsSummaryResponse | null>(
    null
  );
  const [activeSession, setActiveSession] = useState<SessionResponse | null>(
    null
  );
  const [todaySessions, setTodaySessions] = useState<SessionResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [sessionTitle, setSessionTitle] = useState("");
  const [sessionContext, setSessionContext] = useState("");
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionBusy, setActionBusy] = useState(false);
  const [historyOpen, setHistoryOpen] = useState(true);

  const loadTodayData = useCallback(async () => {
    const from = startOfLocalDayIso();
    const to = new Date().toISOString();
    const [summaryData, sessions] = await Promise.all([
      api.getAnalyticsSummary({ from, to }),
      api.getSessions({ page: 1, pageSize: 50, from, to, sortOrder: "desc" }),
    ]);
    setSummary(summaryData);
    setTodaySessions(
      (sessions?.items ?? []).filter((s) => s.endedAtUtc !== undefined)
    );
  }, []);

  const refreshActive = useCallback(async () => {
    const active = await api.getActiveSession();
    setActiveSession(active);
  }, []);

  useEffect(() => {
    let cancelled = false;
    async function initial() {
      setLoading(true);
      await Promise.all([loadTodayData(), refreshActive()]);
      if (!cancelled) setLoading(false);
    }
    void initial();
    return () => {
      cancelled = true;
    };
  }, [loadTodayData, refreshActive]);

  useEffect(() => {
    if (!activeSession) return;
    const id = window.setInterval(() => {
      void refreshActive();
    }, 5000);
    return () => window.clearInterval(id);
  }, [activeSession, refreshActive]);

  useEffect(() => {
    void connectFocusHub({
      onSessionStarted: () => {
        void refreshActive();
        void loadTodayData();
      },
      onSessionEnded: () => {
        void refreshActive();
        void loadTodayData();
      },
      onSessionPaused: () => void refreshActive(),
      onSessionResumed: () => void refreshActive(),
    });
    return () => {
      void disconnectFocusHub();
    };
  }, [refreshActive, loadTodayData]);

  async function handleStart() {
    const title = sessionTitle.trim();
    if (!title) {
      setActionError("Enter a task name to start.");
      return;
    }
    setActionBusy(true);
    setActionError(null);
    const result = await api.startSession({
      sessionTitle: title,
      sessionContext: sessionContext.trim() || undefined,
    });
    setActionBusy(false);
    if (!result.ok) {
      setActionError(
        result.error ?? `Could not start session (${result.status}).`
      );
      return;
    }
    setSessionTitle("");
    setSessionContext("");
    setActiveSession(result.data);
    await loadTodayData();
  }

  async function handlePause() {
    if (!activeSession) return;
    setActionBusy(true);
    setActionError(null);
    const result = await api.pauseSession(activeSession.id);
    setActionBusy(false);
    if (!result.ok) {
      setActionError(result.error ?? "Could not pause session.");
      return;
    }
    setActiveSession(result.data);
  }

  async function handleResume() {
    if (!activeSession) return;
    setActionBusy(true);
    setActionError(null);
    const result = await api.resumeSession(activeSession.id);
    setActionBusy(false);
    if (!result.ok) {
      setActionError(result.error ?? "Could not resume session.");
      return;
    }
    setActiveSession(result.data);
  }

  async function handleEnd() {
    if (!activeSession) return;
    setActionBusy(true);
    setActionError(null);
    const focusedSeconds = computeLiveSessionActiveSeconds(
      activeSession.startedAtUtc,
      activeSession.pausedAtUtc,
      activeSession.totalPausedSeconds,
      Date.now()
    );
    const result = await api.endSession(activeSession.id, {
      focusScorePercent: 100,
      focusedSeconds,
      distractedSeconds: 0,
      distractionCount: 0,
      contextSwitchCount: 0,
    });
    setActionBusy(false);
    if (!result.ok) {
      setActionError(result.error ?? "Could not end session.");
      return;
    }
    setActiveSession(null);
    await loadTodayData();
    await refreshActive();
  }

  if (loading) {
    return (
      <div className="dashboard-page">
        <header className="page-header">
          <h1 className="page-title">Focus Dashboard</h1>
          <p className="page-subtitle">Loading your focus data...</p>
        </header>
      </div>
    );
  }

  const s = summary;
  const hasSessions = (s?.totalSessions ?? 0) > 0;
  const focusPct = s?.averageFocusScorePercent ?? 0;
  const trackedSeconds = s?.totalActiveSeconds ?? 0;
  const avgDistractionSec = averageDistractionDurationSeconds(
    s?.totalDistractedSeconds ?? 0,
    s?.totalDistractionCount ?? 0
  );

  return (
    <div className="dashboard-page">
      <header className="page-header">
        <h1 className="page-title">Focus Dashboard</h1>
        <p className="page-subtitle">
          Your focus command center — today&apos;s stats and session controls
        </p>
      </header>

      {actionError && (
        <div className="dashboard-banner dashboard-banner-error" role="alert">
          {actionError}
        </div>
      )}

      {activeSession ? (
        <section className="dashboard-session-panel dashboard-session-panel-active">
          <div className="dashboard-session-panel-head">
            <div>
              <div className="dashboard-session-label">Active session</div>
              <h2 className="dashboard-session-title">
                {activeSession.sessionTitle}
              </h2>
              <div className="dashboard-session-meta">
                Started {formatDateTime(activeSession.startedAtUtc)}
                {activeSession.isPaused ? " · Paused" : ""}
              </div>
            </div>
            <SessionTimer
              startedAtUtc={activeSession.startedAtUtc}
              pausedAtUtc={activeSession.pausedAtUtc}
              totalPausedSeconds={activeSession.totalPausedSeconds}
            />
          </div>
          <div className="dashboard-session-actions">
            {activeSession.isPaused ? (
              <button
                type="button"
                className="btn-primary"
                disabled={actionBusy}
                onClick={() => void handleResume()}
              >
                Resume
              </button>
            ) : (
              <button
                type="button"
                className="btn-secondary"
                disabled={actionBusy}
                onClick={() => void handlePause()}
              >
                Pause
              </button>
            )}
            <button
              type="button"
              className="btn-danger"
              disabled={actionBusy}
              onClick={() => void handleEnd()}
            >
              End session
            </button>
          </div>
          <p className="dashboard-session-hint">
            Sessions you end here are saved without distraction tracking. For
            full alignment metrics, use the desktop app or browser extension.
          </p>
        </section>
      ) : (
        <section className="dashboard-session-panel dashboard-session-panel-start">
          <h2 className="dashboard-start-heading">Start a focus session</h2>
          <p className="dashboard-start-lead">
            Name your task and start the clock. You can pause or end anytime.
          </p>
          <label className="dashboard-field-label" htmlFor="dash-task">
            Task
          </label>
          <input
            id="dash-task"
            className="dashboard-input"
            value={sessionTitle}
            onChange={(e) => setSessionTitle(e.target.value)}
            placeholder="e.g. Deep work on API design"
            autoComplete="off"
          />
          <label className="dashboard-field-label" htmlFor="dash-context">
            Context (optional)
          </label>
          <textarea
            id="dash-context"
            className="dashboard-textarea"
            value={sessionContext}
            onChange={(e) => setSessionContext(e.target.value)}
            placeholder="Extra notes for yourself"
            rows={3}
          />
          <button
            type="button"
            className="btn-primary dashboard-start-btn"
            disabled={actionBusy}
            onClick={() => void handleStart()}
          >
            Start focus session
          </button>
        </section>
      )}

      <div className="dashboard-kpi-grid">
        <KpiCard
          variant="focus-score"
          label="Focus score"
          value={hasSessions ? undefined : "—"}
          sublabel={
            hasSessions
              ? "Average for completed sessions today"
              : "Complete a session to see your score"
          }
          focusPercentage={hasSessions ? focusPct : 0}
          focusDetails={
            <div className="kpi-focus-detail-row">
              <span className="kpi-focus-detail-aligned">
                Aligned {formatDuration(s?.totalFocusedSeconds ?? 0)}
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
          sublabel="Time aligned with task (today)"
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
            avgDistractionSec !== null
              ? `Avg duration ${formatDuration(Math.round(avgDistractionSec))}`
              : "No distraction episodes today"
          }
        />
      </div>

      <section className="dashboard-history-card">
        <button
          type="button"
          className="dashboard-history-toggle"
          aria-expanded={historyOpen}
          onClick={() => setHistoryOpen((o) => !o)}
        >
          <span className="dashboard-history-toggle-title">Today</span>
          <span className="dashboard-history-toggle-meta">
            {todaySessions.length} session
            {todaySessions.length === 1 ? "" : "s"}
          </span>
          <span className="dashboard-history-chevron" aria-hidden>
            {historyOpen ? "▾" : "▸"}
          </span>
        </button>
        {historyOpen && (
          <div className="dashboard-history-body">
            {todaySessions.length === 0 ? (
              <div className="empty-state">
                No completed sessions yet today.
              </div>
            ) : (
              <table className="dashboard-history-table">
                <thead>
                  <tr>
                    <th>Task</th>
                    <th>Started</th>
                    <th>Focus</th>
                    <th>Duration</th>
                  </tr>
                </thead>
                <tbody>
                  {todaySessions.map((row) => {
                    const dur = computeEndedSessionActiveSeconds(row);
                    const fp = row.focusScorePercent;
                    return (
                      <tr key={row.id}>
                        <td>{row.sessionTitle}</td>
                        <td>{formatDateTime(row.startedAtUtc)}</td>
                        <td
                          className={
                            fp === undefined || fp === null
                              ? ""
                              : `focus-pct-${
                                  fp >= 70 ? "high" : fp >= 40 ? "mid" : "low"
                                }`
                          }
                        >
                          {fp !== undefined && fp !== null ? `${fp}%` : "—"}
                        </td>
                        <td>{formatDuration(dur)}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            )}
          </div>
        )}
      </section>
    </div>
  );
}

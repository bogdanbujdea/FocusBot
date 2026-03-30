import { useEffect, useMemo, useState } from "react";
import { sendRuntimeRequest } from "../shared/runtime";
import type { RuntimeState } from "../shared/types";
import { calculateLiveSummary } from "../shared/metrics";
import { formatSeconds } from "../shared/utils";

interface SessionCardProps {
  state: RuntimeState;
  compact?: boolean;
  onChanged: () => Promise<void>;
}

const classificationToLabel = (
  visitState: "classifying" | "classified" | "error" | undefined,
  classification: "aligned" | "neutral" | "distracting" | undefined,
  score: number | undefined
): string => {
  if (visitState === "classifying") {
    return "Analyzing page...";
  }

  if (visitState === "error") {
    return "Classifier error";
  }

  if (classification === "aligned") {
    return score ? `Aligned (${score}/10)` : "Aligned";
  }
  if (classification === "neutral") {
    return score ? `Neutral (${score}/10)` : "Neutral";
  }
  if (classification === "distracting") {
    return score ? `Distracting (${score}/10)` : "Distracting";
  }
  return "Running";
};

const formatMmSs = (seconds: number): string => {
  const safe = Math.max(0, Math.round(seconds));
  const hours = Math.floor(safe / 3600);
  const minutes = Math.floor((safe % 3600) / 60);
  const remainingSeconds = safe % 60;

  if (hours > 0) {
    return `${hours}:${String(minutes).padStart(2, "0")}:${String(remainingSeconds).padStart(2, "0")}`;
  }

  return `${String(minutes).padStart(2, "0")}:${String(remainingSeconds).padStart(2, "0")}`;
};



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

export const SessionCard = ({ state, compact = false, onChanged }: SessionCardProps): JSX.Element => {
  const [taskText, setTaskText] = useState("");
  const [taskHints, setTaskHints] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [optimisticSession, setOptimisticSession] = useState<{ taskText: string; startedAt: string } | null>(null);
  const [tick, setTick] = useState(0);

  const active = state.activeSession;

  useEffect(() => {
    if (active) {
      setOptimisticSession(null);
    }
  }, [active]);

  const showingActive = Boolean(active) || optimisticSession !== null;
  const displayTaskText = active?.taskText ?? optimisticSession?.taskText ?? "";
  const displayStartedAt = active?.startedAt ?? optimisticSession?.startedAt ?? new Date().toISOString();

  useEffect(() => {
    if (!showingActive) return;
    const id = setInterval(() => setTick((t) => t + 1), 1000);
    return () => clearInterval(id);
  }, [showingActive]);

  const elapsedSeconds = useMemo(() => {
    if (!showingActive) {
      return 0;
    }
    if (!active) {
      return Math.max(0, Math.round((Date.now() - Date.parse(displayStartedAt)) / 1000));
    }
    const totalPaused = active.totalPausedSeconds ?? 0;
    const endMs = active.pausedAt ? Date.parse(active.pausedAt) : Date.now();
    const rawElapsed = (endMs - Date.parse(displayStartedAt)) / 1000;
    return Math.max(0, Math.round(rawElapsed - totalPaused));
  }, [showingActive, active, displayStartedAt, tick]);

  const liveSummary = useMemo(() => {
    if (!active) return null;
    return calculateLiveSummary(active);
  }, [active, tick]);

  const currentState = classificationToLabel(
    active?.currentVisit?.visitState,
    active?.currentVisit?.classification,
    active?.currentVisit?.score
  );
  const isPaused = Boolean(active?.pausedAt);
  const statusClass =
    isPaused
      ? "neutral"
      : active?.currentVisit?.visitState === "classifying"
          ? "neutral"
          : active?.currentVisit?.visitState === "error"
            ? "distracting"
            : (active?.currentVisit?.classification ?? "neutral");
  const displayStatus = active
    ? (isPaused ? (active.pausedBy === "idle" ? "Paused (idle)" : "Paused") : currentState)
    : "Starting...";
  const displayStatusClass = active ? statusClass : "neutral";

  const pillLabel = active
    ? isPaused
      ? active.pausedBy === "idle"
        ? "Paused (idle)"
        : "Paused"
      : displayStatusClass === "aligned"
        ? "Aligned"
        : displayStatusClass === "distracting"
          ? "Distracting"
          : displayStatus
    : displayStatus;

  const pillClass =
    displayStatusClass === "aligned"
      ? "preview-pill-aligned"
      : displayStatusClass === "distracting"
        ? "preview-pill-distracting"
        : "preview-pill-neutral";

  const aiReasonText =
    active && !isPaused
      ? active.currentVisit?.reason ??
        ""
      : "";

  const currentLocationLabel = "Current website";
  const currentLocationValue = active?.currentVisit?.domain ?? "Unknown";

  const liveAlignedSeconds = liveSummary?.alignedSeconds ?? 0;
  const liveDistractingSeconds = liveSummary?.distractingSeconds ?? 0;
  const liveTotalTracked = liveSummary?.totalTrackedSeconds ?? 0;
  const liveDistractedPct = liveTotalTracked > 0 ? (liveDistractingSeconds / liveTotalTracked) * 100 : 0;
  const liveAlignedPct = liveTotalTracked > 0 ? 100 - liveDistractedPct : 100;
  const liveAlignedPctRounded = Math.round(liveAlignedPct);
  const liveDistractedPctRounded = Math.max(0, 100 - liveAlignedPctRounded);

  const startSession = async (): Promise<void> => {
    setBusy(true);
    setError("");
    const response = await sendRuntimeRequest({ type: "START_SESSION", taskText, taskHints: taskHints.trim() || undefined });
    if (!response.ok) {
      setError(response.error ?? "Unable to start session.");
      setBusy(false);
      return;
    }

    setOptimisticSession({ taskText: taskText.trim(), startedAt: new Date().toISOString() });
    setTaskText("");
    setTaskHints("");
    await onChanged();
    setBusy(false);
  };

  const endSession = async (): Promise<void> => {
    setBusy(true);
    setError("");
    const response = await sendRuntimeRequest({ type: "END_SESSION" });
    if (!response.ok) {
      setError(response.error ?? "Unable to end session.");
      setBusy(false);
      return;
    }

    setOptimisticSession(null);
    await onChanged();
    setBusy(false);
  };

  const pauseSession = async (): Promise<void> => {
    setBusy(true);
    setError("");
    const response = await sendRuntimeRequest({ type: "PAUSE_SESSION" });
    if (!response.ok) {
      setError(response.error ?? "Unable to pause session.");
      setBusy(false);
      return;
    }
    await onChanged();
    setBusy(false);
  };

  const resumeSession = async (): Promise<void> => {
    setBusy(true);
    setError("");
    const response = await sendRuntimeRequest({ type: "RESUME_SESSION" });
    if (!response.ok) {
      setError(response.error ?? "Unable to resume session.");
      setBusy(false);
      return;
    }
    await onChanged();
    setBusy(false);
  };

  return (
    <section className="card">
      {showingActive ? (
        <div className="preview-card-header">
          <span className="preview-title">Current Focus Session</span>
          <span className={`preview-pill ${pillClass}`}>{pillLabel}</span>
        </div>
      ) : (
        <h2>{compact ? "Start a task" : "Current Focus Session"}</h2>
      )}
      {showingActive ? (
        <div className="stack">
          {active && liveSummary ? (
            <>
              <div className="preview-context">
                <span className="preview-context-label">{currentLocationLabel}</span>
                <span className="preview-context-value">{currentLocationValue}</span>
              </div>

              <div className="preview-grid">
                <div className="preview-metric">
                  <span className="preview-metric-label">Task</span>
                  <span className="preview-metric-value">{displayTaskText}</span>
                </div>
                <div className="preview-metric">
                  <span className="preview-metric-label">Focus</span>
                  <span className="preview-metric-value preview-metric-value-aligned">{liveAlignedPctRounded}%</span>
                </div>
                <div className="preview-metric">
                  <span className="preview-metric-label">Elapsed</span>
                  <span className="preview-metric-value">{formatMmSs(elapsedSeconds)}</span>
                </div>
                <div className="preview-metric">
                  <span className="preview-metric-label">Focus time</span>
                  <span className="preview-metric-value">{formatMmSs(liveAlignedSeconds)}</span>
                </div>
                <div className="preview-metric">
                  <span className="preview-metric-label">Distracted time</span>
                  <span className="preview-metric-value">{formatMmSs(liveDistractingSeconds)}</span>
                </div>
                <div className="preview-metric">
                  <span className="preview-metric-label">Context switches</span>
                  <span className="preview-metric-value">{liveSummary.distractionCount}</span>
                </div>
                <div className="preview-metric">
                  <span className="preview-metric-label">Avg recovery time</span>
                  <span className="preview-metric-value">{formatMmSs(liveSummary.contextSwitchCostSeconds)}</span>
                </div>
              </div>

              <div
                className="focus-split"
                aria-label={`Focus time split: ${liveAlignedPctRounded} percent focused, ${liveDistractedPctRounded} percent distracted`}
              >
                <div className="focus-split-header">
                  <span className="focus-split-label">Focus split</span>
                </div>

                <div
                  className="focus-split-bar"
                  role="img"
                  aria-label={`Session focus: ${liveAlignedPctRounded}% focused, ${liveDistractedPctRounded}% distracted`}
                >
                  <span
                    className="focus-split-segment focus-split-segment-focused"
                    style={{ width: `${liveAlignedPctRounded}%` }}
                  />
                  <span
                    className="focus-split-segment focus-split-segment-distracted"
                    style={{ width: `${liveDistractedPctRounded}%` }}
                  />
                </div>

                <div className="focus-split-legend" aria-label="Focused and distracted percentages">
                  <div className="focus-split-legend-item">
                    <span className="focus-split-dot focus-split-dot-focused" aria-hidden="true" />
                    <span className="focus-split-legend-text">Focused {liveAlignedPctRounded}%</span>
                  </div>
                  <div className="focus-split-legend-item">
                    <span className="focus-split-dot focus-split-dot-distracted" aria-hidden="true" />
                    <span className="focus-split-legend-text">Distracted {liveDistractedPctRounded}%</span>
                  </div>
                </div>
              </div>

              {aiReasonText ? (
                <div className="status-line">
                  <span
                    className={`status-accent ${
                      displayStatusClass === "distracting" ? "status-accent-distracting" : displayStatusClass === "aligned" ? "status-accent-aligned" : ""
                    }`}
                    aria-hidden="true"
                  />
                  <p className="status-text">
                    <strong>AI Reason:</strong> {aiReasonText}
                  </p>
                </div>
              ) : null}
            </>
          ) : (
            <>
              <p className="muted">
                <strong>Task:</strong> {displayTaskText}
              </p>
              <p className="muted">
                <strong>Elapsed:</strong> {formatSeconds(elapsedSeconds)}
              </p>
              <p className={`status ${displayStatusClass}`}>
                <strong>Status:</strong> {displayStatus}
              </p>
            </>
          )}
          <div className="actions-row">
            {isPaused ? (
              <button disabled={busy} onClick={() => void resumeSession()}>
                Resume
              </button>
            ) : (
              <button disabled={busy} onClick={() => void pauseSession()}>
                Pause
              </button>
            )}
            <button disabled={busy} onClick={() => void endSession()}>
              End Task
            </button>
          </div>
        </div>
      ) : (
        <div className="stack">
          <label htmlFor="task-input" className="label">
            Task
          </label>
          <textarea
            id="task-input"
            placeholder="Example: Review bank statement and mail it to my accountant"
            value={taskText}
            onChange={(event) => setTaskText(event.target.value)}
            rows={compact ? 3 : 4}
          />
          <label htmlFor="context-input" className="label">
            Context (optional)
          </label>
          <p className="muted" style={{ fontSize: "0.85em", marginTop: "-8px", marginBottom: "8px" }}>
            Context helps the AI understand which apps and websites are aligned with your task.
          </p>
          <textarea
            id="context-input"
            placeholder="e.g. Outlook is work email, YouTube for tutorials"
            value={taskHints}
            onChange={(event) => setTaskHints(event.target.value.slice(0, 200))}
            rows={2}
            maxLength={200}
          />
          <button disabled={busy || !taskText.trim()} onClick={() => void startSession()}>
            Start Focus Session
          </button>
        </div>
      )}
      {error ? <p className="error">{error}</p> : null}
    </section>
  );
};

import { useEffect, useMemo, useState } from "react";
import { sendRuntimeRequest } from "../shared/runtime";
import type { RuntimeState } from "../shared/types";
import type { IntegrationState } from "../shared/integrationTypes";
import { formatSeconds } from "../shared/utils";

interface SessionCardProps {
  state: RuntimeState;
  compact?: boolean;
  onChanged: () => Promise<void>;
  integration?: IntegrationState | null;
}

const classificationToLabel = (
  visitState: "classifying" | "classified" | "error" | undefined,
  classification: "aligned" | "distracting" | undefined
): string => {
  if (visitState === "classifying") {
    return "Analyzing page...";
  }

  if (visitState === "error") {
    return "Classifier error";
  }

  if (classification === "aligned") {
    return "Aligned";
  }
  if (classification === "distracting") {
    return "Distracting";
  }
  return "Waiting for signal";
};

export const SessionCard = ({ state, compact = false, onChanged, integration }: SessionCardProps): JSX.Element => {
  const [taskText, setTaskText] = useState("");
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

  const desktopCtx = active ? integration?.currentDesktopContext : undefined;
  const showDesktopContext = Boolean(desktopCtx) && integration?.browserInForeground === false;

  const currentState = showDesktopContext
    ? (desktopCtx!.classification === "aligned" ? "Aligned" : "Distracting")
    : classificationToLabel(active?.currentVisit?.visitState, active?.currentVisit?.classification);
  const isPaused = Boolean(active?.pausedAt);
  const statusClass =
    isPaused
      ? "neutral"
      : showDesktopContext
        ? desktopCtx!.classification
        : active?.currentVisit?.visitState === "classifying"
          ? "neutral"
          : active?.currentVisit?.visitState === "error"
            ? "distracting"
            : (active?.currentVisit?.classification ?? "neutral");
  const displayStatus = active
    ? (isPaused ? "Paused" : currentState)
    : "Starting...";
  const displayStatusClass = active ? statusClass : "neutral";

  const startSession = async (): Promise<void> => {
    setBusy(true);
    setError("");
    const response = await sendRuntimeRequest({ type: "START_SESSION", taskText });
    if (!response.ok) {
      setError(response.error ?? "Unable to start session.");
      setBusy(false);
      return;
    }

    setOptimisticSession({ taskText: taskText.trim(), startedAt: new Date().toISOString() });
    setTaskText("");
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

  if (integration?.leaderTaskId) {
    const status = integration.lastFocusStatus;
    const taskText = integration.leaderTaskText ?? "Waiting for task...";
    const classification = status?.classification ?? "Waiting...";
    const reason = status?.reason ?? "";
    const focusPercent = status?.focusScorePercent ?? 0;
    const contextTitle = status?.contextTitle ?? "";
    const statusClass =
      classification === "aligned" || classification === "Focused" || classification === "Aligned"
        ? "aligned"
        : classification === "distracting" || classification === "Distracted" || classification === "Distracting"
          ? "distracting"
          : "neutral";
    const statusLabel =
      statusClass === "aligned" ? "Aligned" : statusClass === "distracting" ? "Distracting" : "Waiting...";
    const activeAppDisplay = contextTitle ? contextTitle.replace(/^Browser:\s*/i, "").trim() || contextTitle : "";

    return (
      <section className="card">
        <h2>Task in progress</h2>
        <div className="stack">
          <p className="muted">
            <strong>Source:</strong> Desktop App
          </p>
          <p className="muted">
            <strong>Task:</strong> {taskText}
          </p>
          <p className={`status ${statusClass}`}>
            <strong>Status:</strong> {statusLabel}
          </p>
          {reason ? <p className="muted">{reason}</p> : null}
          {activeAppDisplay ? (
            <p className="muted">
              <strong>Active desktop app:</strong> {activeAppDisplay}
            </p>
          ) : null}
          <p className="muted">
            <strong>Focus:</strong> {focusPercent}% Focused
          </p>
        </div>
      </section>
    );
  }

  return (
    <section className="card">
      <h2>{compact ? "Focus Session" : "Current Focus Session"}</h2>
      {showingActive ? (
        <div className="stack">
          <p className="muted">
            <strong>Task:</strong> {displayTaskText}
          </p>
          <p className="muted">
            <strong>Elapsed:</strong> {formatSeconds(elapsedSeconds)}
          </p>
          <p className="muted">
            <strong>Minutes on current task:</strong> {Math.floor(elapsedSeconds / 60)}
          </p>
          <p className={`status ${displayStatusClass}`}>
            <strong>Status:</strong> {displayStatus}
          </p>
          {active?.currentVisit?.reason && !isPaused ? (
            <p className="muted">{active.currentVisit.reason}</p>
          ) : null}
          {showDesktopContext && desktopCtx ? (
            <>
              <p className="muted">
                <strong>Desktop:</strong> {desktopCtx.processName} - {desktopCtx.windowTitle}
              </p>
              {desktopCtx.reason ? <p className="muted">{desktopCtx.reason}</p> : null}
            </>
          ) : null}
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
            One task for this session
          </label>
          <textarea
            id="task-input"
            placeholder="Example: Review bank statement and mail it to my accountant"
            value={taskText}
            onChange={(event) => setTaskText(event.target.value)}
            rows={compact ? 3 : 4}
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

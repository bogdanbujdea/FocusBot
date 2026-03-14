import { useEffect, useMemo, useState } from "react";
import { sendRuntimeRequest } from "../shared/runtime";
import type { RuntimeState } from "../shared/types";
import { formatSeconds } from "../shared/utils";

interface SessionCardProps {
  state: RuntimeState;
  compact?: boolean;
  onChanged: () => Promise<void>;
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

export const SessionCard = ({ state, compact = false, onChanged }: SessionCardProps): JSX.Element => {
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

  const currentState = classificationToLabel(active?.currentVisit?.visitState, active?.currentVisit?.classification);
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

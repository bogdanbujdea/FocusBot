import { useMemo, useState } from "react";
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

  const active = state.activeSession;
  const elapsedSeconds = useMemo(() => {
    if (!active) {
      return 0;
    }
    return Math.max(0, Math.round((Date.now() - Date.parse(active.startedAt)) / 1000));
  }, [active]);

  const currentState = classificationToLabel(active?.currentVisit?.visitState, active?.currentVisit?.classification);
  const statusClass =
    active?.currentVisit?.visitState === "classifying"
      ? "neutral"
      : active?.currentVisit?.visitState === "error"
        ? "distracting"
        : (active?.currentVisit?.classification ?? "neutral");

  const startSession = async (): Promise<void> => {
    setBusy(true);
    setError("");
    const response = await sendRuntimeRequest({ type: "START_SESSION", taskText });
    if (!response.ok) {
      setError(response.error ?? "Unable to start session.");
      setBusy(false);
      return;
    }

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

    await onChanged();
    setBusy(false);
  };

  return (
    <section className="card">
      <h2>{compact ? "Focus Session" : "Current Focus Session"}</h2>
      {active ? (
        <div className="stack">
          <p className="muted">
            <strong>Task:</strong> {active.taskText}
          </p>
          <p className="muted">
            <strong>Elapsed:</strong> {formatSeconds(elapsedSeconds)}
          </p>
          <p className={`status ${statusClass}`}>
            <strong>Status:</strong> {currentState}
          </p>
          {active.currentVisit?.reason ? <p className="muted">{active.currentVisit.reason}</p> : null}
          <button disabled={busy} onClick={() => void endSession()}>
            End Task
          </button>
        </div>
      ) : (
        <div className="stack">
          <label htmlFor="task-input" className="label">
            One task for this session
          </label>
          <textarea
            id="task-input"
            placeholder="Example: Review PRs for payment retry logic"
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

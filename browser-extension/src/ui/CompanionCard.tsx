import type { IntegrationState } from "../shared/integrationTypes";

interface CompanionCardProps {
  integration: IntegrationState;
}

export const CompanionCard = ({ integration }: CompanionCardProps): JSX.Element => {
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
    statusClass === "aligned"
      ? "Aligned"
      : statusClass === "distracting"
        ? "Distracting"
        : "Waiting...";

  const activeAppDisplay = contextTitle
    ? contextTitle.replace(/^Browser:\s*/i, "").trim() || contextTitle
    : "";

  return (
    <section className="card companion-card">
      <h2>Companion Mode</h2>
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
};

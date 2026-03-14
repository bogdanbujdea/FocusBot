import type { IntegrationState } from "../shared/integrationTypes";

interface CompanionCardProps {
  integration: IntegrationState;
}

export const CompanionCard = ({ integration }: CompanionCardProps): JSX.Element => {
  const status = integration.lastFocusStatus;
  const taskText = status?.taskId
    ? (integration.leaderTaskText ?? "Unknown Task")
    : "Waiting for status...";

  const classification = status?.classification ?? "Waiting...";
  const reason = status?.reason ?? "";
  const focusPercent = status?.focusScorePercent ?? 0;
  const contextType = status?.contextType ?? "";
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

  const contextDisplay = contextTitle
    ? `${contextType === "browser" ? "Browser" : "Desktop"}: ${contextTitle}`
    : "";

  return (
    <section className="card companion-card">
      <div className="companion-badge">Desktop App Connected</div>
      <h2>Companion Mode</h2>
      <div className="stack">
        <p className="muted">
          <strong>Following:</strong> {taskText}
        </p>
        <p className={`status ${statusClass}`}>
          <strong>Status:</strong> {statusLabel}
        </p>
        {reason ? <p className="muted">{reason}</p> : null}
        {contextDisplay ? (
          <p className="muted">
            <strong>Context:</strong> {contextDisplay}
          </p>
        ) : null}
        <p className="muted">
          <strong>Focus:</strong> {focusPercent}% Focused
        </p>
      </div>
    </section>
  );
};

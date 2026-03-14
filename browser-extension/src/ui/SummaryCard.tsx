import { sendRuntimeRequest } from "../shared/runtime";
import type { DomainAggregate, RuntimeState } from "../shared/types";
import { formatSeconds } from "../shared/utils";

interface SummaryCardProps {
  state: RuntimeState;
}

const DomainList = ({ domains, emptyText }: { domains: DomainAggregate[]; emptyText: string }): JSX.Element => {
  if (domains.length === 0) {
    return <p className="muted">{emptyText}</p>;
  }

  return (
    <ul className="domain-list">
      {domains.map((domain) => (
        <li key={domain.domain}>
          <span>{domain.domain}</span>
          <span>{formatSeconds(domain.totalSeconds)}</span>
        </li>
      ))}
    </ul>
  );
};

export const SummaryCard = ({ state }: SummaryCardProps): JSX.Element => {
  const summary = state.lastSummary;
  if (!summary) {
    return (
      <section className="card">
        <h2>Session Summary</h2>
        <p className="muted">Complete a session to view your latest deep work summary.</p>
      </section>
    );
  }

  return (
    <section className="card">
      <h2>Latest Session Summary</h2>
      <div className="grid two">
        <p>
          <strong>Focus:</strong> {summary.focusPercentage.toFixed(1)}%
        </p>
        <p>
          <strong>Distractions:</strong> {summary.distractionCount}
        </p>
        <p>
          <strong>Aligned time:</strong> {formatSeconds(summary.alignedSeconds)}
        </p>
        <p>
          <strong>Distracting time:</strong> {formatSeconds(summary.distractingSeconds)}
        </p>
        <p>
          <strong>Context switch cost:</strong> {formatSeconds(summary.contextSwitchCostSeconds)}
        </p>
        <p>
          <strong>Tracked time:</strong> {formatSeconds(summary.totalTrackedSeconds)}
        </p>
      </div>

      <h3>Top distracting domains</h3>
      <DomainList domains={summary.topDistractionDomains} emptyText="No distraction detected." />

      <div className="actions-row">
        <button onClick={() => void sendRuntimeRequest({ type: "OPEN_ANALYTICS" })}>Open Analytics</button>
        <button onClick={() => void sendRuntimeRequest({ type: "OPEN_OPTIONS" })}>Open Settings</button>
      </div>
    </section>
  );
};

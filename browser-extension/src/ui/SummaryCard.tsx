import { useEffect, useMemo, useState } from "react";
import { sendRuntimeRequest } from "../shared/runtime";
import { calculateLiveSummary } from "../shared/metrics";
import type { DomainAggregate, RuntimeState, SessionSummary } from "../shared/types";
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

const SummaryContent = ({
  summary,
  title,
  showActions = true
}: {
  summary: SessionSummary;
  title: string;
  showActions?: boolean;
}): JSX.Element => (
  <>
    <h2>{title}</h2>
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

    {showActions ? (
      <div className="actions-row">
        <button onClick={() => void sendRuntimeRequest({ type: "OPEN_ANALYTICS" })}>Open Analytics</button>
        <button onClick={() => void sendRuntimeRequest({ type: "OPEN_OPTIONS" })}>Open Settings</button>
      </div>
    ) : null}
  </>
);

export const SummaryCard = ({ state }: SummaryCardProps): JSX.Element => {
  const [tick, setTick] = useState(0);

  useEffect(() => {
    if (!state.activeSession) return;
    const id = setInterval(() => setTick((t) => t + 1), 1000);
    return () => clearInterval(id);
  }, [state.activeSession]);

  const liveSummary = useMemo(() => {
    if (!state.activeSession) return null;
    return calculateLiveSummary(state.activeSession);
  }, [state.activeSession, tick]);

  if (state.activeSession && liveSummary) {
    return (
      <section className="card">
        <SummaryContent summary={liveSummary} title="Current Session" showActions={true} />
      </section>
    );
  }

  const summary = state.lastSummary;
  if (!summary) {
    return (
      <section className="card">
        <h2>Session Summary</h2>
        <p className="muted">Complete a session to view your latest session summary.</p>
      </section>
    );
  }

  return (
    <section className="card">
      <SummaryContent summary={summary} title="Latest Session Summary" showActions={true} />
    </section>
  );
};

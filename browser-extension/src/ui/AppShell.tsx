import { sendRuntimeRequest } from "../shared/runtime";
import type { RuntimeState } from "../shared/types";
import { SessionCard } from "./SessionCard";
import { SummaryCard } from "./SummaryCard";

interface AppShellProps {
  title: string;
  description: string;
  state: RuntimeState;
  loading: boolean;
  compact?: boolean;
  refreshState: () => Promise<void>;
}

export const AppShell = ({
  title,
  description,
  state,
  loading,
  compact = false,
  refreshState
}: AppShellProps): JSX.Element => (
  <main className={`app-shell ${compact ? "compact" : ""}`}>
    <header className="app-shell-header">
      <div className="app-shell-header-text">
        <h1>{title}</h1>
        <p className="muted">{description}</p>
      </div>
      <div className="quick-actions">
        <button
          type="button"
          className="quick-action-btn quick-action-btn--analytics"
          onClick={() => void sendRuntimeRequest({ type: "OPEN_ANALYTICS" })}
          title="View Analytics"
        >
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <path d="M18 20V10" />
            <path d="M12 20V4" />
            <path d="M6 20v-6" />
          </svg>
        </button>
        <button
          type="button"
          className="quick-action-btn quick-action-btn--settings"
          onClick={() => void sendRuntimeRequest({ type: "OPEN_OPTIONS" })}
          title="Settings"
        >
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <circle cx="12" cy="12" r="3" />
            <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z" />
          </svg>
        </button>
      </div>
    </header>

    {loading ? <p className="muted">Loading current session...</p> : null}
    {state.lastError ? (
      <section className="card">
        <h2>Attention</h2>
        <p className="error">{state.lastError}</p>
        <div className="actions-row">
          <button onClick={() => void sendRuntimeRequest({ type: "CLEAR_ERROR" })}>Dismiss error</button>
        </div>
      </section>
    ) : null}

    <SessionCard state={state} compact={compact} onChanged={refreshState} />
    <SummaryCard state={state} />
  </main>
);

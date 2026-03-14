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
}: AppShellProps): JSX.Element => {
  const openSidePanel = async (): Promise<void> => {
    try {
      const currentWindow = await chrome.windows.getCurrent();
      if (currentWindow.id) {
        await chrome.sidePanel.open({ windowId: currentWindow.id });
        return;
      }
    } catch {
      // Ignore and fallback to background handling.
    }

    await sendRuntimeRequest({ type: "OPEN_SIDE_PANEL" });
  };

  return (
    <main className={`app-shell ${compact ? "compact" : ""}`}>
      <header>
        <h1>{title}</h1>
        <p className="muted">{description}</p>
      </header>

      {loading ? <p className="muted">Loading current session...</p> : null}

      <SessionCard state={state} compact={compact} onChanged={refreshState} />
      <SummaryCard state={state} />

      <section className="card">
        <h2>Quick Actions</h2>
        <div className="actions-row">
          <button onClick={() => void openSidePanel()}>Open Side Panel</button>
          <button onClick={() => void sendRuntimeRequest({ type: "OPEN_ANALYTICS" })}>View Analytics</button>
          <button onClick={() => void sendRuntimeRequest({ type: "OPEN_OPTIONS" })}>Settings</button>
        </div>
      </section>
    </main>
  );
};

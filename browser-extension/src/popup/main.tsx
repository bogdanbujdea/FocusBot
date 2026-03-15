import { createRoot } from "react-dom/client";
import { AppShell } from "../ui/AppShell";
import "../ui/styles.css";
import { useRuntimeState } from "../ui/useRuntimeState";
import { useIntegrationState } from "../ui/useIntegrationState";

const PopupApp = (): JSX.Element => {
  const { state, loading, refreshState } = useRuntimeState();
  const integration = useIntegrationState();

  return (
    <AppShell
      title="FocusBot Deep Work"
      description="Set one task, track alignment, and review distraction cost."
      state={state}
      loading={loading}
      compact
      refreshState={refreshState}
      integration={integration}
    />
  );
};

const container = document.getElementById("root");
if (!container) {
  throw new Error("Root container not found.");
}

createRoot(container).render(<PopupApp />);

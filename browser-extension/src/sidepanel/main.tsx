import { createRoot } from "react-dom/client";
import { AppShell } from "../ui/AppShell";
import "../ui/styles.css";
import { useRuntimeState } from "../ui/useRuntimeState";
import { useIntegrationState } from "../ui/useIntegrationState";

const SidePanelApp = (): JSX.Element => {
  const { state, loading, refreshState } = useRuntimeState();
  const integration = useIntegrationState();

  return (
    <AppShell
      title="FocusBot Side Panel"
      description="Live focus state while you work."
      state={state}
      loading={loading}
      refreshState={refreshState}
      integration={integration}
      showHeaderMeta={false}
    />
  );
};

const container = document.getElementById("root");
if (!container) {
  throw new Error("Root container not found.");
}

createRoot(container).render(<SidePanelApp />);

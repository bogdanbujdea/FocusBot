import { useCallback, useEffect, useState } from "react";
import { sendRuntimeRequest } from "../shared/runtime";
import type { IntegrationState } from "../shared/integrationTypes";

const defaultState: IntegrationState = {
  mode: "standalone",
  connected: false
};

export const useIntegrationState = (): IntegrationState => {
  const [state, setState] = useState<IntegrationState>(defaultState);

  const refresh = useCallback(async () => {
    const response = await sendRuntimeRequest<IntegrationState>({ type: "GET_INTEGRATION_STATE" });
    if (response.ok && response.data) {
      setState(response.data);
    }
  }, []);

  useEffect(() => {
    void refresh();
    chrome.runtime.sendMessage({ type: "START_DESKTOP_INTEGRATION" }).catch(() => {});

    const listener = (message: unknown): void => {
      if (
        typeof message === "object" &&
        message !== null &&
        "type" in message &&
        (message as { type?: string }).type === "STATE_UPDATED"
      ) {
        void refresh();
      }
    };

    chrome.runtime.onMessage.addListener(listener);
    return () => chrome.runtime.onMessage.removeListener(listener);
  }, [refresh]);

  return state;
};

import { useCallback, useEffect, useMemo, useState } from "react";
import { sendRuntimeRequest } from "../shared/runtime";
import type { RuntimeState } from "../shared/types";

const emptyState: RuntimeState = {
  settings: {
    plan: "free-byok",
    openAiApiKey: "",
    classifierModel: "gpt-4o-mini",
    onboardingCompleted: false,
    excludedDomains: []
  },
  activeSession: null,
  lastSummary: null,
  lastError: null,
  isAuthenticated: false
};

export const useRuntimeState = (): {
  state: RuntimeState;
  loading: boolean;
  refreshState: () => Promise<void>;
} => {
  const [state, setState] = useState<RuntimeState>(emptyState);
  const [loading, setLoading] = useState(true);

  const refreshState = useCallback(async () => {
    const response = await sendRuntimeRequest<RuntimeState>({ type: "GET_STATE" });
    if (response.ok && response.data) {
      setState(response.data);
    }
    setLoading(false);
  }, []);

  useEffect(() => {
    void refreshState();

    const listener = (message: unknown): void => {
      if (
        typeof message === "object" &&
        message !== null &&
        "type" in message &&
        (message as { type?: string }).type === "STATE_UPDATED" &&
        "data" in message
      ) {
        const payload = message as { data: RuntimeState };
        setState(payload.data);
      }
    };

    chrome.runtime.onMessage.addListener(listener);
    return () => chrome.runtime.onMessage.removeListener(listener);
  }, [refreshState]);

  return useMemo(
    () => ({
      state,
      loading,
      refreshState
    }),
    [loading, refreshState, state]
  );
};

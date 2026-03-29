import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from "react";
import { api } from "../api/client";
import type { SubscriptionStatusResponse } from "../api/types";
import { useAuth } from "../auth/useAuth";

interface SubscriptionContextType {
  subscription: SubscriptionStatusResponse | null;
  loading: boolean;
  error: string | null;
  refresh: () => Promise<void>;
}

const SubscriptionContext = createContext<SubscriptionContextType | undefined>(
  undefined
);

export function SubscriptionProvider({ children }: { children: ReactNode }) {
  const { session } = useAuth();
  const [subscription, setSubscription] =
    useState<SubscriptionStatusResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    setError(null);
    try {
      const status = await api.getSubscriptionStatus();
      setSubscription(status);
    } catch {
      setError("Failed to load subscription status.");
    }
  }, []);

  useEffect(() => {
    if (!session) {
      setSubscription(null);
      setLoading(false);
      return;
    }

    setLoading(true);
    void refresh().finally(() => setLoading(false));
  }, [session, refresh]);

  return (
    <SubscriptionContext.Provider value={{ subscription, loading, error, refresh }}>
      {children}
    </SubscriptionContext.Provider>
  );
}

export function useSubscription(): SubscriptionContextType {
  const ctx = useContext(SubscriptionContext);
  if (!ctx)
    throw new Error("useSubscription must be used within SubscriptionProvider");
  return ctx;
}

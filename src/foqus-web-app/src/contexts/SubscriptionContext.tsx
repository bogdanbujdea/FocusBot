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
import { loadSubscriptionForUser } from "./subscriptionBootstrap";

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
  const { user } = useAuth();
  const userId = user?.id;
  const [subscription, setSubscription] =
    useState<SubscriptionStatusResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    setError(null);
    const uid = user?.id;
    if (!uid) {
      setSubscription(null);
      return;
    }
    try {
      await api.getMe();
      const status = await api.getSubscriptionStatus();
      setSubscription(status);
    } catch {
      setError("Failed to load subscription status.");
    }
  }, [user?.id]);

  useEffect(() => {
    if (!userId) {
      setSubscription(null);
      setLoading(false);
      setError(null);
      return;
    }

    let cancelled = false;
    setLoading(true);
    setError(null);

    void loadSubscriptionForUser(userId)
      .then((status) => {
        if (!cancelled) setSubscription(status);
      })
      .catch(() => {
        if (!cancelled) setError("Failed to load subscription status.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [userId]);

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

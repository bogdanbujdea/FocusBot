import { api } from "../api/client";
import type { SubscriptionStatusResponse } from "../api/types";

const bootstrapPromises = new Map<
  string,
  Promise<SubscriptionStatusResponse | null>
>();

/**
 * Provisions the app user via GET /auth/me, then loads subscription status.
 * Single-flight per user id so Supabase session updates and React Strict Mode
 * do not multiply identical requests.
 */
export function loadSubscriptionForUser(
  userId: string
): Promise<SubscriptionStatusResponse | null> {
  let p = bootstrapPromises.get(userId);
  if (!p) {
    p = (async () => {
      await api.getMe();
      return await api.getSubscriptionStatus();
    })();
    bootstrapPromises.set(userId, p);
    p.finally(() => {
      bootstrapPromises.delete(userId);
    });
  }
  return p;
}

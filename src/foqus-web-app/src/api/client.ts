import { supabase } from "../auth/supabase";
import type {
  MeResponse,
  SubscriptionStatusResponse,
  SessionResponse,
  PaginatedResponse,
  AnalyticsSummaryResponse,
  AnalyticsTrendsResponse,
  AnalyticsDevicesResponse,
  ApiMutationResult,
  StartSessionRequest,
  EndSessionRequest,
} from "./types";

const API_BASE_URL =
  (import.meta.env.VITE_API_BASE_URL as string | undefined)?.trim() ||
  (import.meta.env.DEV ? "http://localhost:5251" : "https://api.foqus.me");

async function apiFetch<T>(
  path: string,
  init: RequestInit = {}
): Promise<T | null> {
  const {
    data: { session },
  } = await supabase.auth.getSession();
  const token = session?.access_token;

  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...(init.headers as Record<string, string> | undefined),
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };

  const url = `${API_BASE_URL}${path}`;

  const response = await fetch(url, { ...init, headers });

  if (!response.ok) {
    if (response.status === 401) {
      const { error } = await supabase.auth.refreshSession();
      if (!error) {
        const {
          data: { session: refreshed },
        } = await supabase.auth.getSession();
        if (refreshed?.access_token) {
          headers["Authorization"] = `Bearer ${refreshed.access_token}`;
          const retry = await fetch(url, { ...init, headers });
          if (retry.ok) {
            const text = await retry.text();
            return text ? (JSON.parse(text) as T) : null;
          }
        }
      }
    }
    return null;
  }

  const text = await response.text();
  return text ? (JSON.parse(text) as T) : null;
}

async function parseErrorMessage(response: Response): Promise<string | undefined> {
  try {
    const text = await response.text();
    if (!text) return undefined;
    const j = JSON.parse(text) as { error?: string };
    return typeof j.error === "string" ? j.error : undefined;
  } catch {
    return undefined;
  }
}

async function apiMutate<T>(
  path: string,
  init: RequestInit
): Promise<ApiMutationResult<T>> {
  const {
    data: { session },
  } = await supabase.auth.getSession();
  const token = session?.access_token;

  const headers: Record<string, string> = {
    ...(init.headers as Record<string, string> | undefined),
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
  if (init.body !== undefined && init.body !== null && init.body !== "") {
    headers["Content-Type"] = "application/json";
  }

  const url = `${API_BASE_URL}${path}`;

  const doFetch = async (hdrs: Record<string, string>) =>
    fetch(url, { ...init, headers: hdrs });

  let response = await doFetch(headers);

  if (!response.ok && response.status === 401) {
    const { error } = await supabase.auth.refreshSession();
    if (!error) {
      const {
        data: { session: refreshed },
      } = await supabase.auth.getSession();
      if (refreshed?.access_token) {
        headers["Authorization"] = `Bearer ${refreshed.access_token}`;
        response = await doFetch(headers);
      }
    }
  }

  if (!response.ok) {
    const error = await parseErrorMessage(response);
    return { ok: false, status: response.status, error };
  }

  const text = await response.text();
  const data = (text ? JSON.parse(text) : null) as T;
  return { ok: true, data };
}

function buildQuery(params: Record<string, string | undefined>): string {
  const entries = Object.entries(params).filter(
    (e): e is [string, string] => e[1] !== undefined && e[1] !== ""
  );
  return entries.length > 0
    ? "?" + new URLSearchParams(entries).toString()
    : "";
}

export const api = {
  getMe: () => apiFetch<MeResponse>("/auth/me"),

  deleteAccount: () =>
    apiFetch<{ message: string }>("/auth/account", { method: "DELETE" }),

  getSubscriptionStatus: () =>
    apiFetch<SubscriptionStatusResponse>("/subscriptions/status"),

  activateTrial: () =>
    apiFetch<{ status: string; trialEndsAt: string }>("/subscriptions/trial", {
      method: "POST",
    }),

  getActiveSession: () => apiFetch<SessionResponse>("/sessions/active"),

  getSessions: (params?: {
    page?: number;
    pageSize?: number;
    deviceId?: string;
    from?: string;
    to?: string;
    sessionTitle?: string;
    sortBy?: string;
    sortOrder?: string;
  }) => {
    const query = buildQuery({
      page: params?.page?.toString(),
      pageSize: params?.pageSize?.toString(),
      deviceId: params?.deviceId,
      from: params?.from,
      to: params?.to,
      sessionTitle: params?.sessionTitle,
      sortBy: params?.sortBy,
      sortOrder: params?.sortOrder,
    });
    return apiFetch<PaginatedResponse<SessionResponse>>(`/sessions${query}`);
  },

  getSession: (id: string) => apiFetch<SessionResponse>(`/sessions/${id}`),

  startSession: (req: StartSessionRequest) =>
    apiMutate<SessionResponse>("/sessions", {
      method: "POST",
      body: JSON.stringify({
        sessionTitle: req.sessionTitle,
        sessionContext: req.sessionContext ?? null,
        deviceId: req.deviceId ?? null,
      }),
    }),

  endSession: (id: string, req: EndSessionRequest) =>
    apiMutate<SessionResponse>(`/sessions/${encodeURIComponent(id)}/end`, {
      method: "POST",
      body: JSON.stringify({
        focusScorePercent: req.focusScorePercent,
        focusedSeconds: req.focusedSeconds,
        distractedSeconds: req.distractedSeconds,
        distractionCount: req.distractionCount,
        contextSwitchCount: req.contextSwitchCount,
        deviceId: req.deviceId ?? null,
      }),
    }),

  pauseSession: (id: string) =>
    apiMutate<SessionResponse>(`/sessions/${encodeURIComponent(id)}/pause`, {
      method: "POST",
    }),

  resumeSession: (id: string) =>
    apiMutate<SessionResponse>(`/sessions/${encodeURIComponent(id)}/resume`, {
      method: "POST",
    }),

  getAnalyticsSummary: (params?: {
    from?: string;
    to?: string;
    deviceId?: string;
  }) => {
    const query = buildQuery({
      from: params?.from,
      to: params?.to,
      deviceId: params?.deviceId,
    });
    return apiFetch<AnalyticsSummaryResponse>(`/analytics/summary${query}`);
  },

  getAnalyticsTrends: (params?: {
    from?: string;
    to?: string;
    granularity?: string;
    deviceId?: string;
  }) => {
    const query = buildQuery({
      from: params?.from,
      to: params?.to,
      granularity: params?.granularity,
      deviceId: params?.deviceId,
    });
    return apiFetch<AnalyticsTrendsResponse>(`/analytics/trends${query}`);
  },

  getAnalyticsDevices: (params?: { from?: string; to?: string }) => {
    const query = buildQuery({
      from: params?.from,
      to: params?.to,
    });
    return apiFetch<AnalyticsDevicesResponse>(`/analytics/devices${query}`);
  },
};

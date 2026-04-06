import { loadFocusbotAuthSession, refreshFocusbotAuthToken } from "./focusbotAuth";
import { APP_KEYS } from "./utils";

const getWebApiBaseUrl = (): string => {
  const configured = (import.meta as unknown as { env?: { VITE_FOQUS_API_BASE_URL?: string } }).env
    ?.VITE_FOQUS_API_BASE_URL;
  if (configured?.trim()) return configured.trim().replace(/\/+$/, "");

  const isDev = Boolean(
    (import.meta as unknown as { env?: { DEV?: boolean } }).env?.DEV
  );
  return isDev ? "http://localhost:5251" : "https://api.foqus.me";
};

let _baseUrlOverride: string | null = null;

export const setApiBaseUrl = (url: string): void => {
  _baseUrlOverride = url;
};

export const getApiBaseUrl = (): string => _baseUrlOverride ?? getWebApiBaseUrl();

// ---------------------------------------------------------------------------
// Core fetch wrapper
// ---------------------------------------------------------------------------

/**
 * Authenticated fetch using the stored Supabase access token.
 * Automatically refreshes the token on 401 and retries once.
 * Returns null on non-OK responses rather than throwing.
 */
const apiFetch = async <T>(path: string, init: RequestInit = {}): Promise<T | null> => {
  const session = await loadFocusbotAuthSession();

  const buildHeaders = (token: string | undefined): Record<string, string> => ({
    "Content-Type": "application/json",
    ...(init.headers as Record<string, string> | undefined),
    ...(token ? { Authorization: `Bearer ${token}` } : {})
  });

  const url = `${getApiBaseUrl()}${path}`;

  try {
    let response = await fetch(url, { ...init, headers: buildHeaders(session?.accessToken) });

    if (response.status === 401) {
      const refreshed = await refreshFocusbotAuthToken();
      if (refreshed) {
        const updatedSession = await loadFocusbotAuthSession();
        response = await fetch(url, { ...init, headers: buildHeaders(updatedSession?.accessToken) });
      }
    }

    if (!response.ok) {
      console.warn(`[Foqus API] ${init.method ?? "GET"} ${path} failed: ${response.status}`);
      return null;
    }

    const text = await response.text();
    if (!text) return null;
    return JSON.parse(text) as T;
  } catch (error) {
    console.warn(`[Foqus API] ${init.method ?? "GET"} ${path} error:`, error);
    return null;
  }
};

// ---------------------------------------------------------------------------
// Classification
// ---------------------------------------------------------------------------

export type WebApiClassifyRequest = {
  taskText: string;
  taskHints?: string;
  processName?: string;
  windowTitle?: string;
  url?: string;
  pageTitle?: string;
  providerId?: string;
  modelId?: string;
};

export type WebApiClassifyResponse = {
  score: number;
  reason: string;
  cached: boolean;
};

const tryGetStoredClientId = async (): Promise<string | undefined> => {
  const r = await chrome.storage.local.get(APP_KEYS.clientId);
  const v = r[APP_KEYS.clientId];
  return typeof v === "string" && v.trim() ? v : undefined;
};

export const classifyViaWebApi = async (
  request: WebApiClassifyRequest,
  options?: { byokApiKey?: string }
): Promise<WebApiClassifyResponse> => {
  const clientId = await tryGetStoredClientId();
  const body = {
    sessionTitle: request.taskText,
    sessionContext: request.taskHints ?? null,
    processName: request.processName ?? null,
    windowTitle: request.windowTitle ?? null,
    url: request.url ?? null,
    pageTitle: request.pageTitle ?? null,
    providerId: request.providerId ?? null,
    modelId: request.modelId ?? null,
    ...(clientId ? { clientId } : {})
  };

  const byok = options?.byokApiKey?.trim();
  const extraHeaders: Record<string, string> = {};
  if (byok) {
    extraHeaders["X-Api-Key"] = byok;
  }

  const result = await apiFetch<WebApiClassifyResponse>("/classify", {
    method: "POST",
    body: JSON.stringify(body),
    headers: extraHeaders
  });

  if (!result) {
    throw new Error("Foqus classification failed. Ensure you are signed in and have an active plan.");
  }

  return result;
};

// ---------------------------------------------------------------------------
// Auth / user
// ---------------------------------------------------------------------------

export interface MeResponse {
  userId: string;
  email: string;
  subscriptionStatus: string;
}

export const fetchCurrentUser = async (): Promise<MeResponse | null> =>
  apiFetch<MeResponse>("/auth/me");

// ---------------------------------------------------------------------------
// Subscription
// ---------------------------------------------------------------------------

/** Integer values match the server-side PlanType enum (FreeBYOK=0, CloudBYOK=1, CloudManaged=2). */
export type BackendPlanType = 0 | 1 | 2;

export interface SubscriptionStatusResponse {
  status: string;
  planType: BackendPlanType;
  trialEndsAt?: string;
  currentPeriodEndsAt?: string;
}

export const getSubscriptionStatus = async (): Promise<SubscriptionStatusResponse | null> =>
  apiFetch<SubscriptionStatusResponse>("/subscriptions/status");

// ---------------------------------------------------------------------------
// Sessions
// ---------------------------------------------------------------------------

export interface StartSessionResponse {
  id: string;
  sessionTitle: string;
  sessionContext?: string;
  startedAtUtc: string;
}

export interface EndSessionResponse {
  id: string;
  endedAtUtc: string;
}

export interface ActiveSessionResponse {
  id: string;
  sessionTitle: string;
  sessionContext?: string;
  clientId?: string;
  startedAtUtc: string;
  endedAtUtc?: string;
  pausedAtUtc?: string;
  totalPausedSeconds: number;
  isPaused: boolean;
  source: string;
}

export const startCloudSession = async (
  taskText: string,
  clientId: string | null,
  taskHints?: string
): Promise<StartSessionResponse | null> =>
  apiFetch<StartSessionResponse>("/sessions", {
    method: "POST",
    body: JSON.stringify({
      sessionTitle: taskText,
      sessionContext: taskHints ?? null,
      clientId: clientId ?? null
    })
  });

/** Server already ended the session (409), or session is missing (404) — local UI should still clear. */
export type EndCloudSessionResult =
  | { kind: "ended"; response: EndSessionResponse }
  | { kind: "alreadyGone" }
  | { kind: "failed" };

export const endCloudSession = async (
  sessionId: string,
  summary: Record<string, unknown>,
  clientId: string | null
): Promise<EndCloudSessionResult> => {
  const focusPercentage = typeof summary.focusPercentage === "number" ? summary.focusPercentage : 0;
  const alignedSeconds = typeof summary.alignedSeconds === "number" ? summary.alignedSeconds : 0;
  const distractingSeconds = typeof summary.distractingSeconds === "number" ? summary.distractingSeconds : 0;
  const distractionCount = typeof summary.distractionCount === "number" ? summary.distractionCount : 0;

  const session = await loadFocusbotAuthSession();

  const buildHeaders = (token: string | undefined): Record<string, string> => ({
    "Content-Type": "application/json",
    ...(token ? { Authorization: `Bearer ${token}` } : {})
  });

  const path = `/sessions/${encodeURIComponent(sessionId)}/end`;
  const url = `${getApiBaseUrl()}${path}`;
  const body = JSON.stringify({
    focusScorePercent: Math.round(focusPercentage),
    focusedSeconds: Math.round(alignedSeconds),
    distractedSeconds: Math.round(distractingSeconds),
    distractionCount,
    contextSwitchCount: 0,
    clientId: clientId ?? null
  });

  try {
    let response = await fetch(url, {
      method: "POST",
      headers: buildHeaders(session?.accessToken),
      body
    });

    if (response.status === 401) {
      const refreshed = await refreshFocusbotAuthToken();
      if (refreshed) {
        const updatedSession = await loadFocusbotAuthSession();
        response = await fetch(url, {
          method: "POST",
          headers: buildHeaders(updatedSession?.accessToken),
          body
        });
      }
    }

    if (response.ok) {
      const text = await response.text();
      if (!text) {
        return { kind: "failed" };
      }
      const parsed = JSON.parse(text) as EndSessionResponse;
      return { kind: "ended", response: parsed };
    }

    if (response.status === 409 || response.status === 404) {
      return { kind: "alreadyGone" };
    }

    console.warn(`[Foqus API] POST ${path} failed: ${response.status}`);
    return { kind: "failed" };
  } catch (error) {
    console.warn(`[Foqus API] POST ${path} error:`, error);
    return { kind: "failed" };
  }
};

export const getActiveCloudSession = async (): Promise<ActiveSessionResponse | null> =>
  apiFetch<ActiveSessionResponse>("/sessions/active");

export const pauseCloudSession = async (
  sessionId: string
): Promise<ActiveSessionResponse | null> =>
  apiFetch<ActiveSessionResponse>(`/sessions/${encodeURIComponent(sessionId)}/pause`, {
    method: "POST"
  });

export const resumeCloudSession = async (
  sessionId: string
): Promise<ActiveSessionResponse | null> =>
  apiFetch<ActiveSessionResponse>(`/sessions/${encodeURIComponent(sessionId)}/resume`, {
    method: "POST"
  });

// ---------------------------------------------------------------------------
// Clients
// ---------------------------------------------------------------------------

export type ClientTypeName = "Desktop" | "Extension";

/** Matches API ClientHost enum: Unknown=0, Windows=1, Chrome=2, Edge=3 */
export type ClientHostValue = 0 | 1 | 2 | 3;

export const getClientHostFromUserAgent = (): ClientHostValue => {
  const ua = navigator.userAgent;
  if (ua.includes("Edg/")) return 3;
  if (ua.includes("Chrome/")) return 2;
  return 0;
};

export interface RegisterClientRequest {
  clientType: ClientTypeName;
  host: ClientHostValue;
  name: string;
  fingerprint: string;
  appVersion: string;
  platform: string;
}

export interface RegisterClientResponse {
  id: string;
  clientType: number;
  host: number;
  name: string;
  fingerprint: string;
  appVersion: string | null;
  platform: string | null;
  ipAddress: string | null;
  lastSeenAtUtc: string;
  createdAtUtc: string;
  isOnline: boolean;
}

export const registerClient = async (
  request: RegisterClientRequest
): Promise<RegisterClientResponse | null> => {
  const clientTypeInt = request.clientType === "Desktop" ? 1 : 2;
  return apiFetch<RegisterClientResponse>("/clients", {
    method: "POST",
    body: JSON.stringify({
      clientType: clientTypeInt,
      host: request.host,
      name: request.name,
      fingerprint: request.fingerprint,
      appVersion: request.appVersion,
      platform: request.platform
    })
  });
};

export const deregisterClient = async (clientId: string): Promise<boolean> => {
  const result = await apiFetch<unknown>(`/clients/${clientId}`, {
    method: "DELETE"
  });
  return result !== null;
};

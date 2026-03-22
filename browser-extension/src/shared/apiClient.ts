import { loadFocusbotAuthSession, refreshFocusbotAuthToken } from "./focusbotAuth";

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

export const classifyViaWebApi = async (request: WebApiClassifyRequest): Promise<WebApiClassifyResponse> => {
  const result = await apiFetch<WebApiClassifyResponse>("/classify", {
    method: "POST",
    body: JSON.stringify(request)
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

export const startCloudSession = async (
  taskText: string,
  deviceId: string | null,
  taskHints?: string
): Promise<StartSessionResponse | null> =>
  apiFetch<StartSessionResponse>("/sessions", {
    method: "POST",
    body: JSON.stringify({
      sessionTitle: taskText,
      sessionContext: taskHints ?? null,
      deviceId: deviceId ?? null
    })
  });

export const endCloudSession = async (
  sessionId: string,
  summary: Record<string, unknown>,
  deviceId: string | null
): Promise<EndSessionResponse | null> => {
  const focusPercentage = typeof summary.focusPercentage === "number" ? summary.focusPercentage : 0;
  const alignedSeconds = typeof summary.alignedSeconds === "number" ? summary.alignedSeconds : 0;
  const distractingSeconds = typeof summary.distractingSeconds === "number" ? summary.distractingSeconds : 0;
  const distractionCount = typeof summary.distractionCount === "number" ? summary.distractionCount : 0;

  return apiFetch<EndSessionResponse>(`/sessions/${sessionId}/end`, {
    method: "POST",
    body: JSON.stringify({
      focusScorePercent: Math.round(focusPercentage),
      focusedSeconds: Math.round(alignedSeconds),
      distractedSeconds: Math.round(distractingSeconds),
      distractionCount,
      contextSwitchCount: 0,
      deviceId: deviceId ?? null
    })
  });
};

// ---------------------------------------------------------------------------
// Devices
// ---------------------------------------------------------------------------

export type DeviceType = "Desktop" | "Extension";

export interface RegisterDeviceRequest {
  deviceType: DeviceType;
  name: string;
  fingerprint: string;
  appVersion: string;
  platform: string;
}

export interface RegisterDeviceResponse {
  id: string;
  name: string;
  deviceType: number;
  lastSeenAtUtc: string;
}

export const registerDevice = async (
  request: RegisterDeviceRequest
): Promise<RegisterDeviceResponse | null> => {
  const deviceTypeInt = request.deviceType === "Desktop" ? 1 : 2;
  return apiFetch<RegisterDeviceResponse>("/devices", {
    method: "POST",
    body: JSON.stringify({
      deviceType: deviceTypeInt,
      name: request.name,
      fingerprint: request.fingerprint,
      appVersion: request.appVersion,
      platform: request.platform
    })
  });
};

export const sendHeartbeat = async (deviceId: string): Promise<boolean> => {
  const result = await apiFetch<unknown>(`/devices/${deviceId}/heartbeat`, {
    method: "PUT"
  });
  return result !== null;
};

export const deregisterDevice = async (deviceId: string): Promise<boolean> => {
  const result = await apiFetch<unknown>(`/devices/${deviceId}`, {
    method: "DELETE"
  });
  return result !== null;
};

// ---------------------------------------------------------------------------
// Legacy shims — kept so existing call sites and tests continue to compile
// ---------------------------------------------------------------------------

/** @deprecated Use startCloudSession. */
export const startSession = async (
  taskText: string,
  taskHints?: string
): Promise<StartSessionResponse | null> => startCloudSession(taskText, null, taskHints);

/** @deprecated Use endCloudSession. */
export const endSession = async (
  sessionId: string,
  summary: Record<string, unknown>
): Promise<EndSessionResponse | null> => endCloudSession(sessionId, summary, null);

/** @deprecated Use fetchCurrentUser. */
export const getMe = async (): Promise<{ id: string; email: string } | null> => {
  const me = await fetchCurrentUser();
  if (!me) return null;
  return { id: me.userId, email: me.email };
};

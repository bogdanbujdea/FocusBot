import { loadFocusbotAuthSession } from "./focusbotAuth";

const API_BASE_URL = "http://localhost:5251";

async function authorizedFetch(input: string, init: RequestInit = {}): Promise<Response> {
  const session = await loadFocusbotAuthSession();
  const headers = new Headers(init.headers ?? {});

  if (session?.accessToken) {
    headers.set("Authorization", `Bearer ${session.accessToken}`);
  }

  if (!headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  return fetch(`${API_BASE_URL}${input}`, {
    ...init,
    headers
  });
}

export interface MeResponse {
  userId: string;
  email: string;
  subscriptionStatus: string;
}

export const fetchCurrentUser = async (): Promise<MeResponse> => {
  const response = await authorizedFetch("/auth/me", {
    method: "GET"
  });

  if (response.status === 401) {
    throw new Error("Not authenticated with Foqus. Please complete sign-in.");
  }

  if (!response.ok) {
    const text = await response.text();
    throw new Error(`Failed to load Foqus account. ${response.status} ${text}`);
  }

  return (await response.json()) as MeResponse;
};

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

const getWebApiBaseUrl = (): string => {
  const configured = (import.meta as any)?.env?.VITE_FOQUS_API_BASE_URL as string | undefined;
  if (configured && configured.trim()) return configured.trim().replace(/\/+$/, "");

  const isDev = Boolean((import.meta as any)?.env?.DEV);
  return isDev ? "http://localhost:5251" : "https://api.foqus.me";
};

export const classifyViaWebApi = async (request: WebApiClassifyRequest): Promise<WebApiClassifyResponse> => {
  const session = await loadFocusbotAuthSession();
  if (!session?.accessToken) {
    throw new Error("Not authenticated with Foqus. Please complete sign-in.");
  }

  const baseUrl = getWebApiBaseUrl();
  const response = await fetch(`${baseUrl}/classify`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${session.accessToken}`,
      "Content-Type": "application/json"
    },
    body: JSON.stringify(request)
  });

  if (response.status === 401) {
    throw new Error("Not authenticated with Foqus. Please complete sign-in.");
  }

  if (!response.ok) {
    const text = await response.text().catch(() => "");
    throw new Error(`Foqus classification failed. ${response.status} ${text}`.trim());
  }

  return (await response.json()) as WebApiClassifyResponse;
};

import { getAccessToken, refreshAccessToken } from "./authToken";

const DEFAULT_BASE_URL = "https://api.foqus.me";

let baseUrl = DEFAULT_BASE_URL;

export const setApiBaseUrl = (url: string): void => {
  baseUrl = url;
};

export const getApiBaseUrl = (): string => baseUrl;

interface ClassifyRequest {
  url: string;
  title: string;
  taskText: string;
  taskHints?: string;
  apiKey?: string;
}

interface ClassifyResponse {
  classification: string;
  confidence: number;
  reason?: string;
}

interface StartSessionRequest {
  taskText: string;
  taskHints?: string;
}

interface StartSessionResponse {
  sessionId: string;
  taskText: string;
  taskHints?: string;
  startedAt: string;
}

interface EndSessionRequest {
  summary: Record<string, unknown>;
}

interface EndSessionResponse {
  sessionId: string;
  endedAt: string;
}

interface SubscriptionStatusResponse {
  plan: string;
  active: boolean;
  expiresAt?: string;
}

interface MeResponse {
  id: string;
  email: string;
}

const authHeaders = async (): Promise<Record<string, string>> => {
  const token = await getAccessToken();
  if (!token) return {};
  return { Authorization: `Bearer ${token}` };
};

const apiFetch = async <T>(
  path: string,
  init: RequestInit = {}
): Promise<T | null> => {
  const url = `${baseUrl}${path}`;
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...(init.headers as Record<string, string> | undefined),
    ...(await authHeaders())
  };

  try {
    let response = await fetch(url, { ...init, headers });

    if (response.status === 401) {
      const refreshed = await refreshAccessToken();
      if (refreshed) {
        const retryHeaders: Record<string, string> = {
          ...headers,
          ...(await authHeaders())
        };
        response = await fetch(url, { ...init, headers: retryHeaders });
      }
    }

    if (!response.ok) {
      console.warn(`[Foqus API] ${init.method ?? "GET"} ${path} failed with status ${response.status}`);
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

export const classify = async (request: ClassifyRequest): Promise<ClassifyResponse | null> => {
  const extraHeaders: Record<string, string> = {};
  if (request.apiKey) {
    extraHeaders["X-Api-Key"] = request.apiKey;
  }
  return apiFetch<ClassifyResponse>("/classify", {
    method: "POST",
    headers: extraHeaders,
    body: JSON.stringify({
      url: request.url,
      title: request.title,
      taskText: request.taskText,
      taskHints: request.taskHints
    })
  });
};

export const startSession = async (
  taskText: string,
  taskHints?: string
): Promise<StartSessionResponse | null> =>
  apiFetch<StartSessionResponse>("/sessions", {
    method: "POST",
    body: JSON.stringify({ taskText, taskHints } satisfies StartSessionRequest)
  });

export const endSession = async (
  sessionId: string,
  summary: Record<string, unknown>
): Promise<EndSessionResponse | null> =>
  apiFetch<EndSessionResponse>(`/sessions/${sessionId}/end`, {
    method: "POST",
    body: JSON.stringify({ summary } satisfies EndSessionRequest)
  });

export const getSubscriptionStatus = async (): Promise<SubscriptionStatusResponse | null> =>
  apiFetch<SubscriptionStatusResponse>("/subscriptions/status");

export const getMe = async (): Promise<MeResponse | null> =>
  apiFetch<MeResponse>("/auth/me");

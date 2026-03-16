import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  classify,
  startSession,
  endSession,
  getSubscriptionStatus,
  getMe,
  setApiBaseUrl
} from "../src/shared/apiClient";

const store: Record<string, unknown> = {};

const mockFetch = vi.fn<(input: RequestInfo | URL, init?: RequestInit) => Promise<Response>>();

beforeEach(() => {
  vi.stubGlobal("fetch", mockFetch);
  vi.stubGlobal("chrome", {
    storage: {
      local: {
        get: vi.fn((key: string | string[]) => {
          const keys = typeof key === "string" ? [key] : key;
          const result: Record<string, unknown> = {};
          for (const k of keys) {
            if (k in store) result[k] = store[k];
          }
          return Promise.resolve(result);
        }),
        set: vi.fn((obj: Record<string, unknown>) => {
          for (const [k, v] of Object.entries(obj)) store[k] = v;
          return Promise.resolve();
        }),
        remove: vi.fn((key: string | string[]) => {
          const keys = typeof key === "string" ? [key] : key;
          for (const k of keys) delete store[k];
          return Promise.resolve();
        })
      }
    }
  });

  setApiBaseUrl("https://test.focusbot.app");
  store["focusbot.accessToken"] = "test-access-token";
  store["focusbot.refreshToken"] = "test-refresh-token";
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  for (const key of Object.keys(store)) delete store[key];
});

const jsonResponse = (body: unknown, status = 200): Response =>
  new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" }
  });

describe("classify", () => {
  it("sends correct POST request with auth header", async () => {
    const responseBody = { classification: "aligned", confidence: 0.9, reason: "on task" };
    mockFetch.mockResolvedValueOnce(jsonResponse(responseBody));

    const result = await classify({
      url: "https://example.com",
      title: "Example",
      taskText: "research"
    });

    expect(result).toEqual(responseBody);
    expect(mockFetch).toHaveBeenCalledTimes(1);

    const [url, init] = mockFetch.mock.calls[0];
    expect(url).toBe("https://test.focusbot.app/classify");
    expect(init?.method).toBe("POST");

    const headers = init?.headers as Record<string, string>;
    expect(headers["Authorization"]).toBe("Bearer test-access-token");
    expect(headers["Content-Type"]).toBe("application/json");
  });

  it("includes X-Api-Key header when apiKey provided", async () => {
    mockFetch.mockResolvedValueOnce(jsonResponse({ classification: "aligned", confidence: 0.8 }));

    await classify({
      url: "https://example.com",
      title: "Example",
      taskText: "research",
      apiKey: "my-api-key"
    });

    const [, init] = mockFetch.mock.calls[0];
    const headers = init?.headers as Record<string, string>;
    expect(headers["X-Api-Key"]).toBe("my-api-key");
  });
});

describe("startSession", () => {
  it("sends POST to /sessions with taskText and taskHints", async () => {
    const responseBody = {
      sessionId: "s1",
      taskText: "code review",
      startedAt: "2026-03-16T00:00:00Z"
    };
    mockFetch.mockResolvedValueOnce(jsonResponse(responseBody));

    const result = await startSession("code review", "PR #42");

    expect(result).toEqual(responseBody);
    const [url, init] = mockFetch.mock.calls[0];
    expect(url).toBe("https://test.focusbot.app/sessions");
    expect(init?.method).toBe("POST");

    const body = JSON.parse(init?.body as string);
    expect(body.taskText).toBe("code review");
    expect(body.taskHints).toBe("PR #42");
  });
});

describe("endSession", () => {
  it("sends POST to /sessions/{id}/end", async () => {
    const responseBody = { sessionId: "s1", endedAt: "2026-03-16T01:00:00Z" };
    mockFetch.mockResolvedValueOnce(jsonResponse(responseBody));

    const result = await endSession("s1", { focusPercentage: 85 });

    expect(result).toEqual(responseBody);
    const [url, init] = mockFetch.mock.calls[0];
    expect(url).toBe("https://test.focusbot.app/sessions/s1/end");
    expect(init?.method).toBe("POST");
  });
});

describe("getSubscriptionStatus", () => {
  it("sends GET to /subscriptions/status", async () => {
    const responseBody = { plan: "pro", active: true };
    mockFetch.mockResolvedValueOnce(jsonResponse(responseBody));

    const result = await getSubscriptionStatus();

    expect(result).toEqual(responseBody);
    const [url] = mockFetch.mock.calls[0];
    expect(url).toBe("https://test.focusbot.app/subscriptions/status");
  });
});

describe("getMe", () => {
  it("sends GET to /auth/me", async () => {
    const responseBody = { id: "u1", email: "user@test.com" };
    mockFetch.mockResolvedValueOnce(jsonResponse(responseBody));

    const result = await getMe();

    expect(result).toEqual(responseBody);
    const [url] = mockFetch.mock.calls[0];
    expect(url).toBe("https://test.focusbot.app/auth/me");
  });
});

describe("error handling", () => {
  it("returns null on non-ok response", async () => {
    mockFetch.mockResolvedValueOnce(new Response("Not found", { status: 404 }));

    const result = await getMe();

    expect(result).toBeNull();
  });

  it("returns null on fetch error", async () => {
    mockFetch.mockRejectedValueOnce(new Error("Network error"));

    const result = await getMe();

    expect(result).toBeNull();
  });
});

describe("401 retry", () => {
  it("retries with refreshed token on 401", async () => {
    const refreshResponse = jsonResponse({
      accessToken: "new-access-token",
      refreshToken: "new-refresh-token"
    });
    const meResponse = jsonResponse({ id: "u1", email: "user@test.com" });

    mockFetch
      .mockResolvedValueOnce(new Response("Unauthorized", { status: 401 }))
      .mockResolvedValueOnce(refreshResponse)
      .mockResolvedValueOnce(meResponse);

    const result = await getMe();

    expect(result).toEqual({ id: "u1", email: "user@test.com" });
    expect(mockFetch).toHaveBeenCalledTimes(3);

    const [refreshUrl] = mockFetch.mock.calls[1];
    expect(refreshUrl).toBe("https://api.focusbot.app/auth/refresh");
  });

  it("returns null when refresh fails on 401", async () => {
    mockFetch
      .mockResolvedValueOnce(new Response("Unauthorized", { status: 401 }))
      .mockResolvedValueOnce(new Response("Forbidden", { status: 403 }));

    const result = await getMe();

    expect(result).toBeNull();
  });
});

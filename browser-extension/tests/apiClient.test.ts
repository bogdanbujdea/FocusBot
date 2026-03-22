import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  classifyViaWebApi,
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

  setApiBaseUrl("https://test.foqus.me");
  store["focusbot.supabaseAccessToken"] = "test-access-token";
  store["focusbot.supabaseRefreshToken"] = "test-refresh-token";
  store["focusbot.supabaseEmail"] = "user@test.com";
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

describe("classifyViaWebApi", () => {
  it("sends correct POST request with auth header", async () => {
    const responseBody = { score: 0.9, reason: "on task", cached: false };
    mockFetch.mockResolvedValueOnce(jsonResponse(responseBody));

    const result = await classifyViaWebApi({
      url: "https://example.com",
      pageTitle: "Example",
      taskText: "research"
    });

    expect(result).toEqual(responseBody);
    expect(mockFetch).toHaveBeenCalledTimes(1);

    const [url, init] = mockFetch.mock.calls[0];
    expect(url).toBe("https://test.foqus.me/classify");
    expect(init?.method).toBe("POST");

    const headers = init?.headers as Record<string, string>;
    expect(headers["Authorization"]).toBe("Bearer test-access-token");
    expect(headers["Content-Type"]).toBe("application/json");
  });

  it("throws when API returns null (error)", async () => {
    mockFetch.mockResolvedValueOnce(new Response("Unauthorized", { status: 401 }));
    mockFetch.mockResolvedValueOnce(new Response("Forbidden", { status: 403 }));

    await expect(
      classifyViaWebApi({ taskText: "research", url: "https://example.com" })
    ).rejects.toThrow("Foqus classification failed");
  });
});

describe("startSession", () => {
  it("sends POST to /sessions with taskText and taskHints", async () => {
    const responseBody = {
      id: "s1",
      sessionTitle: "code review",
      startedAtUtc: "2026-03-16T00:00:00Z"
    };
    mockFetch.mockResolvedValueOnce(jsonResponse(responseBody));

    const result = await startSession("code review", "PR #42");

    expect(result).toEqual(responseBody);
    const [url, init] = mockFetch.mock.calls[0];
    expect(url).toBe("https://test.foqus.me/sessions");
    expect(init?.method).toBe("POST");

    const body = JSON.parse(init?.body as string);
    expect(body.sessionTitle).toBe("code review");
    expect(body.sessionContext).toBe("PR #42");
  });
});

describe("endSession", () => {
  it("sends POST to /sessions/{id}/end", async () => {
    const responseBody = { id: "s1", endedAtUtc: "2026-03-16T01:00:00Z" };
    mockFetch.mockResolvedValueOnce(jsonResponse(responseBody));

    const result = await endSession("s1", { focusPercentage: 85 });

    expect(result).toEqual(responseBody);
    const [url, init] = mockFetch.mock.calls[0];
    expect(url).toBe("https://test.foqus.me/sessions/s1/end");
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
    expect(url).toBe("https://test.foqus.me/subscriptions/status");
  });
});

describe("getMe", () => {
  it("sends GET to /auth/me", async () => {
    const responseBody = { userId: "u1", email: "user@test.com", subscriptionStatus: "active" };
    mockFetch.mockResolvedValueOnce(jsonResponse(responseBody));

    const result = await getMe();

    expect(result).toEqual({ id: "u1", email: "user@test.com" });
    const [url] = mockFetch.mock.calls[0];
    expect(url).toBe("https://test.foqus.me/auth/me");
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
      access_token: "new-access-token",
      refresh_token: "new-refresh-token"
    });
    const meResponse = jsonResponse({ userId: "u1", email: "user@test.com", subscriptionStatus: "active" });

    mockFetch
      .mockResolvedValueOnce(new Response("Unauthorized", { status: 401 }))
      .mockResolvedValueOnce(refreshResponse)
      .mockResolvedValueOnce(meResponse);

    const result = await getMe();

    expect(result).toEqual({ id: "u1", email: "user@test.com" });
    expect(mockFetch).toHaveBeenCalledTimes(3);

    const [refreshUrl] = mockFetch.mock.calls[1];
    expect(refreshUrl).toContain("supabase.co/auth/v1/token");
  });

  it("returns null when refresh fails on 401", async () => {
    mockFetch
      .mockResolvedValueOnce(new Response("Unauthorized", { status: 401 }))
      .mockResolvedValueOnce(new Response("Forbidden", { status: 403 }));

    const result = await getMe();

    expect(result).toBeNull();
  });
});

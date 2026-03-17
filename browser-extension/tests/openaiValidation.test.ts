import { describe, expect, it, vi } from "vitest";
import { validateOpenAiKey } from "../src/shared/openaiValidation";

const jsonResponse = (body: unknown, status = 200): Response =>
  new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" }
  });

describe("validateOpenAiKey", () => {
  it("sends a minimal chat completion request with bearer auth", async () => {
    const mockFetch = vi.fn<(input: RequestInfo | URL, init?: RequestInit) => Promise<Response>>();
    vi.stubGlobal("fetch", mockFetch);
    mockFetch.mockResolvedValueOnce(
      jsonResponse({
        choices: [{ message: { content: "pong" } }]
      })
    );

    const result = await validateOpenAiKey({ apiKey: "sk-test", model: "gpt-4o-mini", timeoutMs: 50_000 });

    expect(result).toEqual({ ok: true });
    expect(mockFetch).toHaveBeenCalledTimes(1);

    const [url, init] = mockFetch.mock.calls[0];
    expect(url).toBe("https://api.openai.com/v1/chat/completions");
    expect(init?.method).toBe("POST");
    const headers = init?.headers as Record<string, string>;
    expect(headers.Authorization).toBe("Bearer sk-test");
    expect(headers["Content-Type"]).toBe("application/json");

    const body = JSON.parse(init?.body as string) as {
      model: string;
      max_completion_tokens: number;
      temperature: number;
      messages: Array<{ role: string; content: string }>;
    };
    expect(body.model).toBe("gpt-4o-mini");
    expect(body.max_completion_tokens).toBe(4);
    expect(body.temperature).toBe(0.1);
    expect(body.messages[0]).toEqual({ role: "user", content: "Ping" });

    vi.unstubAllGlobals();
  });

  it("returns friendly invalid key message on 401", async () => {
    const mockFetch = vi.fn<(input: RequestInfo | URL, init?: RequestInit) => Promise<Response>>();
    vi.stubGlobal("fetch", mockFetch);
    mockFetch.mockResolvedValueOnce(
      jsonResponse({ error: { message: "invalid_api_key" } }, 401)
    );

    const result = await validateOpenAiKey({ apiKey: "sk-bad", model: "gpt-4o-mini" });

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error).toContain("The API key may be incorrect");
    }

    vi.unstubAllGlobals();
  });

  it("returns friendly quota message on 429", async () => {
    const mockFetch = vi.fn<(input: RequestInfo | URL, init?: RequestInit) => Promise<Response>>();
    vi.stubGlobal("fetch", mockFetch);
    mockFetch.mockResolvedValueOnce(
      jsonResponse({ error: { message: "429: rate limit" } }, 429)
    );

    const result = await validateOpenAiKey({ apiKey: "sk-rate", model: "gpt-4o-mini" });

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error).toContain("exceeded your API quota");
    }

    vi.unstubAllGlobals();
  });

  it("returns no-response message when content is empty", async () => {
    const mockFetch = vi.fn<(input: RequestInfo | URL, init?: RequestInit) => Promise<Response>>();
    vi.stubGlobal("fetch", mockFetch);
    mockFetch.mockResolvedValueOnce(
      jsonResponse({ choices: [{ message: { content: "" } }] }, 200)
    );

    const result = await validateOpenAiKey({ apiKey: "sk-test", model: "gpt-4o-mini" });

    expect(result).toEqual({ ok: false, error: "No response from API." });

    vi.unstubAllGlobals();
  });
});


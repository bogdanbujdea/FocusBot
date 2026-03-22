import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { api } from "./client";

const sampleSession = {
  id: "a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11",
  sessionTitle: "Task",
  sessionContext: null,
  deviceId: null,
  startedAtUtc: "2025-01-01T10:00:00Z",
  endedAtUtc: null,
  pausedAtUtc: null,
  totalPausedSeconds: 0,
  isPaused: false,
  focusScorePercent: null,
  focusedSeconds: null,
  distractedSeconds: null,
  distractionCount: null,
  contextSwitchCount: null,
  source: "Web",
};

describe("api session mutations", () => {
  beforeEach(() => {
    globalThis.fetch = vi.fn();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("startSession sends POST /sessions with JSON body", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(
      new Response(JSON.stringify(sampleSession), { status: 201 })
    );
    const result = await api.startSession({ sessionTitle: "Deep work" });
    expect(result.ok).toBe(true);
    if (result.ok) expect(result.data.sessionTitle).toBe("Task");
    expect(fetch).toHaveBeenCalledWith(
      expect.stringMatching(/\/sessions$/),
      expect.objectContaining({
        method: "POST",
        headers: expect.objectContaining({
          Authorization: "Bearer test-access-token",
          "Content-Type": "application/json",
        }),
        body: JSON.stringify({
          sessionTitle: "Deep work",
          sessionContext: null,
          deviceId: null,
        }),
      })
    );
  });

  it("endSession sends POST /sessions/{id}/end with stats", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(
      new Response(JSON.stringify(sampleSession), { status: 200 })
    );
    const id = sampleSession.id;
    const result = await api.endSession(id, {
      focusScorePercent: 100,
      focusedSeconds: 120,
      distractedSeconds: 0,
      distractionCount: 0,
      contextSwitchCount: 0,
    });
    expect(result.ok).toBe(true);
    expect(fetch).toHaveBeenCalledWith(
      expect.stringContaining(`/sessions/${id}/end`),
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({
          focusScorePercent: 100,
          focusedSeconds: 120,
          distractedSeconds: 0,
          distractionCount: 0,
          contextSwitchCount: 0,
          deviceId: null,
        }),
      })
    );
  });

  it("pauseSession sends POST without body", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(
      new Response(JSON.stringify(sampleSession), { status: 200 })
    );
    const id = sampleSession.id;
    const result = await api.pauseSession(id);
    expect(result.ok).toBe(true);
    const call = vi.mocked(fetch).mock.calls[0];
    expect(call[0]).toContain(`/sessions/${id}/pause`);
    expect((call[1] as RequestInit).method).toBe("POST");
    expect((call[1] as RequestInit).body).toBeUndefined();
  });

  it("resumeSession sends POST without body", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(
      new Response(JSON.stringify(sampleSession), { status: 200 })
    );
    const id = sampleSession.id;
    const result = await api.resumeSession(id);
    expect(result.ok).toBe(true);
    const call = vi.mocked(fetch).mock.calls[0];
    expect(call[0]).toContain(`/sessions/${id}/resume`);
  });

  it("returns error payload on conflict", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(
      new Response(JSON.stringify({ error: "Already active" }), {
        status: 409,
      })
    );
    const result = await api.startSession({ sessionTitle: "x" });
    expect(result.ok).toBe(false);
    if (!result.ok) expect(result.error).toBe("Already active");
  });
});

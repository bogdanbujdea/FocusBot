import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  loadCompletedSessions,
  pruneOldSessions,
  saveCompletedSession,
  setCompletedSessions
} from "../src/shared/storage";
import type { CompletedSession } from "../src/shared/types";

const createCompletedSession = (
  sessionId: string,
  endedAt: string,
  alignedSeconds: number,
  distractingSeconds: number
): CompletedSession => ({
  sessionId,
  taskText: "Test Task",
  startedAt: endedAt,
  endedAt,
  summary: {
    taskName: "Test Task",
    totalSessionSeconds: alignedSeconds + distractingSeconds,
    totalTrackedSeconds: alignedSeconds + distractingSeconds,
    alignedSeconds,
    distractingSeconds,
    distractionCount: distractingSeconds > 0 ? 1 : 0,
    focusPercentage:
      alignedSeconds + distractingSeconds === 0
        ? 0
        : (alignedSeconds / (alignedSeconds + distractingSeconds)) * 100,
    contextSwitchCostSeconds: distractingSeconds,
    topDistractionDomains: [],
    topAlignedDomains: []
  }
});

describe("completed sessions storage", () => {
  const store: Record<string, unknown> = {};

  beforeEach(() => {
    vi.stubGlobal("chrome", {
      storage: {
        local: {
          get: vi.fn((key: string | string[] | null) => {
            if (key === null || (typeof key === "object" && key.length === 0)) {
              return Promise.resolve({ ...store });
            }
            const keys = typeof key === "string" ? [key] : key;
            const result: Record<string, unknown> = {};
            for (const k of keys) {
              if (k in store) {
                result[k] = store[k];
              }
            }
            return Promise.resolve(result);
          }),
          set: vi.fn((obj: Record<string, unknown>) => {
            for (const [k, v] of Object.entries(obj)) {
              store[k] = v;
            }
            return Promise.resolve();
          }),
          remove: vi.fn((key: string) => {
            delete store[key];
            return Promise.resolve();
          })
        }
      }
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    for (const key of Object.keys(store)) {
      delete store[key];
    }
  });

  it("loadCompletedSessions returns empty array when no data", async () => {
    const sessions = await loadCompletedSessions();
    expect(sessions).toEqual([]);
  });

  it("saveCompletedSession adds session and sorts by endedAt descending", async () => {
    const older = createCompletedSession("s1", "2026-03-14T10:00:00.000Z", 100, 20);
    const newer = createCompletedSession("s2", "2026-03-15T12:00:00.000Z", 80, 10);

    await saveCompletedSession(older);
    await saveCompletedSession(newer);

    const sessions = await loadCompletedSessions();
    expect(sessions).toHaveLength(2);
    expect(sessions[0].sessionId).toBe("s2");
    expect(sessions[1].sessionId).toBe("s1");
  });

  it("saveCompletedSession upserts by sessionId", async () => {
    const s1 = createCompletedSession("s1", "2026-03-14T10:00:00.000Z", 100, 20);
    await saveCompletedSession(s1);

    const s1Updated = createCompletedSession("s1", "2026-03-14T10:00:00.000Z", 120, 30);
    s1Updated.taskText = "Updated Task";
    await saveCompletedSession(s1Updated);

    const sessions = await loadCompletedSessions();
    expect(sessions).toHaveLength(1);
    expect(sessions[0].taskText).toBe("Updated Task");
  });

  it("setCompletedSessions replaces all completed sessions", async () => {
    const list = [
      createCompletedSession("a", "2026-03-14T10:00:00.000Z", 60, 10),
      createCompletedSession("b", "2026-03-14T11:00:00.000Z", 90, 5)
    ];
    await setCompletedSessions(list);

    const sessions = await loadCompletedSessions();
    expect(sessions).toHaveLength(2);
    expect(sessions.map((s) => s.sessionId).sort()).toEqual(["a", "b"]);
  });

  it("pruneOldSessions removes sessions older than maxAgeDays", async () => {
    const now = new Date();
    const oldDate = new Date(now);
    oldDate.setDate(oldDate.getDate() - 100);
    const recentDate = new Date(now);
    recentDate.setDate(recentDate.getDate() - 5);

    const sessions = [
      createCompletedSession("old", oldDate.toISOString(), 100, 20),
      createCompletedSession("recent", recentDate.toISOString(), 80, 10)
    ];
    await setCompletedSessions(sessions);

    await pruneOldSessions(100, 90);

    const after = await loadCompletedSessions();
    expect(after).toHaveLength(1);
    expect(after[0].sessionId).toBe("recent");
  });

  it("pruneOldSessions keeps only maxCount most recent sessions", async () => {
    const now = new Date();
    const sessions: CompletedSession[] = [];
    for (let i = 0; i < 5; i++) {
      const d = new Date(now);
      d.setDate(d.getDate() - i);
      sessions.push(
        createCompletedSession(`s${i}`, d.toISOString(), 60, 10)
      );
    }
    sessions.sort((a, b) => Date.parse(b.endedAt) - Date.parse(a.endedAt));
    await setCompletedSessions(sessions);

    await pruneOldSessions(3, 365);

    const after = await loadCompletedSessions();
    expect(after).toHaveLength(3);
    expect(after[0].sessionId).toBe("s0");
    expect(after[2].sessionId).toBe("s2");
  });
});

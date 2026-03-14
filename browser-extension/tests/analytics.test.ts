import { describe, expect, it } from "vitest";
import { calculateAnalytics } from "../src/shared/analytics";
import type { FocusSession } from "../src/shared/types";

const now = new Date();
const todayIso = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 10, 0, 0).toISOString();
const yesterdayIso = new Date(now.getFullYear(), now.getMonth(), now.getDate() - 1, 10, 0, 0).toISOString();

const session = (sessionId: string, endedAt: string, aligned: number, distracting: number): FocusSession => ({
  sessionId,
  taskText: "Test",
  startedAt: endedAt,
  endedAt,
  visits: [
    {
      pageVisitId: `${sessionId}-1`,
      sessionId,
      tabId: 1,
      url: "https://github.com",
      domain: "github.com",
      title: "GitHub",
      enteredAt: endedAt,
      leftAt: endedAt,
      durationSeconds: aligned,
      classification: "aligned",
      confidence: 0.9
    },
    {
      pageVisitId: `${sessionId}-2`,
      sessionId,
      tabId: 1,
      url: "https://youtube.com",
      domain: "youtube.com",
      title: "YouTube",
      enteredAt: endedAt,
      leftAt: endedAt,
      durationSeconds: distracting,
      classification: "distracting",
      confidence: 0.9
    }
  ],
  summary: {
    taskName: "Test",
    totalSessionSeconds: aligned + distracting,
    totalTrackedSeconds: aligned + distracting,
    alignedSeconds: aligned,
    distractingSeconds: distracting,
    distractionCount: distracting > 0 ? 1 : 0,
    focusPercentage: aligned + distracting === 0 ? 0 : (aligned / (aligned + distracting)) * 100,
    contextSwitchCostSeconds: distracting,
    topDistractionDomains: [
      {
        domain: "youtube.com",
        totalSeconds: distracting,
        visitCount: 1
      }
    ],
    topAlignedDomains: [
      {
        domain: "github.com",
        totalSeconds: aligned,
        visitCount: 1
      }
    ]
  }
});

describe("calculateAnalytics", () => {
  it("aggregates totals for last 7 days", () => {
    const response = calculateAnalytics("7d", [
      session("today", todayIso, 120, 30),
      session("yesterday", yesterdayIso, 90, 10)
    ]);

    expect(response.totals.totalSessions).toBe(2);
    expect(response.totals.totalTrackedSeconds).toBe(250);
    expect(response.totals.totalAlignedSeconds).toBe(210);
    expect(response.totals.totalDistractingSeconds).toBe(40);
    expect(response.totals.distractionCount).toBe(2);
    expect(response.totals.averageContextSwitchCostSeconds).toBe(20);
    expect(response.totals.focusPercentage).toBeCloseTo(84, 0);
    expect(response.totals.mostCommonDistractingDomains[0].domain).toBe("youtube.com");
  });

  it("returns empty totals when no completed sessions are in range", () => {
    const oldIso = new Date(now.getFullYear(), now.getMonth(), now.getDate() - 40, 10, 0, 0).toISOString();
    const response = calculateAnalytics("30d", [session("old", oldIso, 100, 20)]);

    expect(response.totals.totalSessions).toBe(0);
    expect(response.totals.totalTrackedSeconds).toBe(0);
    expect(response.totals.focusPercentage).toBe(0);
    expect(response.statsByDay.length).toBe(30);
  });
});

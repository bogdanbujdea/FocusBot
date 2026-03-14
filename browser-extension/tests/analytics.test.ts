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

  it("handles 'today' range with single day", () => {
    const response = calculateAnalytics("today", [session("today", todayIso, 100, 20)]);

    expect(response.statsByDay.length).toBe(1);
    expect(response.totals.totalSessions).toBe(1);
    expect(response.totals.totalTrackedSeconds).toBe(120);
  });

  it("handles '30d' range", () => {
    const response = calculateAnalytics("30d", [session("today", todayIso, 100, 20)]);

    expect(response.statsByDay.length).toBe(30);
    expect(response.totals.totalSessions).toBe(1);
  });

  it("excludes sessions without endedAt", () => {
    const incompleteSession: FocusSession = {
      sessionId: "incomplete",
      taskText: "Incomplete",
      startedAt: todayIso,
      visits: [],
      summary: {
        taskName: "Incomplete",
        totalSessionSeconds: 100,
        totalTrackedSeconds: 100,
        alignedSeconds: 100,
        distractingSeconds: 0,
        distractionCount: 0,
        focusPercentage: 100,
        contextSwitchCostSeconds: 0,
        topDistractionDomains: [],
        topAlignedDomains: []
      }
    };

    const response = calculateAnalytics("7d", [incompleteSession, session("complete", todayIso, 50, 10)]);

    expect(response.totals.totalSessions).toBe(1);
  });

  it("excludes sessions without summary", () => {
    const noSummarySession: FocusSession = {
      sessionId: "no-summary",
      taskText: "No Summary",
      startedAt: todayIso,
      endedAt: todayIso,
      visits: []
    };

    const response = calculateAnalytics("7d", [noSummarySession, session("with-summary", todayIso, 80, 20)]);

    expect(response.totals.totalSessions).toBe(1);
    expect(response.totals.totalTrackedSeconds).toBe(100);
  });

  it("aggregates multiple sessions on same day", () => {
    const morningIso = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 9, 0, 0).toISOString();
    const afternoonIso = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 14, 0, 0).toISOString();
    const eveningIso = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 18, 0, 0).toISOString();

    const response = calculateAnalytics("today", [
      session("morning", morningIso, 120, 30),
      session("afternoon", afternoonIso, 90, 10),
      session("evening", eveningIso, 60, 20)
    ]);

    expect(response.totals.totalSessions).toBe(3);
    expect(response.totals.totalTrackedSeconds).toBe(330);
    expect(response.totals.totalAlignedSeconds).toBe(270);
    expect(response.totals.totalDistractingSeconds).toBe(60);
    expect(response.statsByDay[0].totalSessions).toBe(3);
  });

  it("returns correct from and to date range", () => {
    const response = calculateAnalytics("7d", []);

    expect(response.range).toBe("7d");
    expect(new Date(response.from)).toBeInstanceOf(Date);
    expect(new Date(response.to)).toBeInstanceOf(Date);
    expect(new Date(response.to) > new Date(response.from)).toBe(true);
  });

  it("limits recentSessions to 10 entries", () => {
    const sessions: FocusSession[] = [];
    for (let i = 0; i < 15; i++) {
      const timestamp = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 10 + i, 0, 0).toISOString();
      sessions.push(session(`session-${i}`, timestamp, 30, 10));
    }

    const response = calculateAnalytics("today", sessions);

    expect(response.recentSessions.length).toBe(10);
  });

  it("sorts recentSessions by startedAt descending", () => {
    const earlyIso = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 8, 0, 0).toISOString();
    const lateIso = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 16, 0, 0).toISOString();

    const response = calculateAnalytics("today", [
      session("early", earlyIso, 60, 10),
      session("late", lateIso, 60, 10)
    ]);

    expect(response.recentSessions[0].sessionId).toBe("late");
    expect(response.recentSessions[1].sessionId).toBe("early");
  });

  it("returns empty insights when no sessions have tracked time", () => {
    const response = calculateAnalytics("7d", []);

    expect(response.insights.bestFocusDay).toBeNull();
    expect(response.insights.averageSessionLengthSeconds).toBe(0);
  });

  it("identifies best focus day correctly", () => {
    const twoDaysAgoIso = new Date(now.getFullYear(), now.getMonth(), now.getDate() - 2, 10, 0, 0).toISOString();

    const response = calculateAnalytics("7d", [
      session("bad-day", yesterdayIso, 30, 70),
      session("good-day", twoDaysAgoIso, 90, 10)
    ]);

    expect(response.insights.bestFocusDay).not.toBeNull();
    expect(response.insights.bestFocusDay?.focusPercentage).toBe(90);
  });

  it("calculates average session length correctly", () => {
    const response = calculateAnalytics("7d", [
      session("a", todayIso, 100, 20),
      session("b", yesterdayIso, 80, 20)
    ]);

    expect(response.insights.averageSessionLengthSeconds).toBe(110);
  });

  it("aggregates domain statistics across multiple sessions", () => {
    const response = calculateAnalytics("today", [
      session("a", todayIso, 100, 50),
      session("b", todayIso, 80, 30)
    ]);

    expect(response.totals.mostCommonDistractingDomains[0].domain).toBe("youtube.com");
    expect(response.totals.mostCommonDistractingDomains[0].totalSeconds).toBe(80);
    expect(response.totals.mostCommonDistractingDomains[0].visitCount).toBe(2);
  });

  it("calculates daily focus percentage correctly", () => {
    const response = calculateAnalytics("today", [session("today", todayIso, 80, 20)]);

    expect(response.statsByDay[0].focusPercentage).toBe(80);
    expect(response.statsByDay[0].averageContextSwitchCostSeconds).toBe(20);
  });
});

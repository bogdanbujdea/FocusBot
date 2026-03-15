import { describe, expect, it } from "vitest";
import { calculateLiveSummary, calculateSessionSummary } from "../src/shared/metrics";
import type { FocusSession, InProgressVisit, PageVisit } from "../src/shared/types";

const createVisit = (overrides: Partial<PageVisit>): PageVisit => ({
  pageVisitId: "visit-1",
  sessionId: "session-1",
  tabId: 12,
  url: "https://example.com",
  domain: "example.com",
  title: "Example",
  enteredAt: "2026-03-14T10:00:00.000Z",
  leftAt: "2026-03-14T10:01:00.000Z",
  durationSeconds: 60,
  classification: "aligned",
  confidence: 0.9,
  reason: "Test",
  ...overrides
});

const createSession = (overrides: Partial<FocusSession>): FocusSession => ({
  sessionId: "session-1",
  taskText: "Test task",
  startedAt: "2026-03-14T10:00:00.000Z",
  visits: [],
  ...overrides
});

const createInProgressVisit = (overrides: Partial<InProgressVisit>): InProgressVisit => ({
  visitToken: "visit-token-1",
  tabId: 1,
  url: "https://github.com",
  domain: "github.com",
  title: "GitHub",
  enteredAt: "2026-03-14T10:00:00.000Z",
  visitState: "classified",
  classification: "aligned",
  confidence: 0.9,
  reason: "Test",
  ...overrides
});

describe("calculateSessionSummary", () => {
  it("computes focus percentage and distraction metrics", () => {
    const visits: PageVisit[] = [
      createVisit({ pageVisitId: "a", domain: "github.com", durationSeconds: 120, classification: "aligned" }),
      createVisit({ pageVisitId: "b", domain: "youtube.com", durationSeconds: 30, classification: "distracting" }),
      createVisit({ pageVisitId: "c", domain: "docs.microsoft.com", durationSeconds: 90, classification: "aligned" }),
      createVisit({ pageVisitId: "d", domain: "news.ycombinator.com", durationSeconds: 45, classification: "distracting" })
    ];

    const summary = calculateSessionSummary(
      "Review pull request",
      "2026-03-14T10:00:00.000Z",
      "2026-03-14T10:10:00.000Z",
      visits
    );

    expect(summary.totalSessionSeconds).toBe(600);
    expect(summary.totalTrackedSeconds).toBe(285);
    expect(summary.alignedSeconds).toBe(210);
    expect(summary.distractingSeconds).toBe(75);
    expect(summary.distractionCount).toBe(2);
    expect(summary.focusPercentage).toBeCloseTo(73.684, 2);
    expect(summary.contextSwitchCostSeconds).toBe(38);
    expect(summary.topDistractionDomains[0].domain).toBe("news.ycombinator.com");
  });

  it("counts distraction only once for consecutive distracting visits", () => {
    const visits: PageVisit[] = [
      createVisit({ pageVisitId: "a", durationSeconds: 30, classification: "aligned" }),
      createVisit({ pageVisitId: "b", durationSeconds: 20, classification: "distracting" }),
      createVisit({ pageVisitId: "c", durationSeconds: 15, classification: "distracting" })
    ];

    const summary = calculateSessionSummary(
      "Write report",
      "2026-03-14T12:00:00.000Z",
      "2026-03-14T12:05:00.000Z",
      visits
    );

    expect(summary.distractionCount).toBe(1);
    expect(summary.contextSwitchCostSeconds).toBe(35);
  });

  it("returns 0% focus for empty visits array", () => {
    const summary = calculateSessionSummary(
      "Empty session",
      "2026-03-14T10:00:00.000Z",
      "2026-03-14T10:10:00.000Z",
      []
    );

    expect(summary.totalTrackedSeconds).toBe(0);
    expect(summary.alignedSeconds).toBe(0);
    expect(summary.distractingSeconds).toBe(0);
    expect(summary.focusPercentage).toBe(0);
    expect(summary.distractionCount).toBe(0);
    expect(summary.contextSwitchCostSeconds).toBe(0);
    expect(summary.topDistractionDomains).toEqual([]);
    expect(summary.topAlignedDomains).toEqual([]);
  });

  it("returns 100% focus when all visits are aligned", () => {
    const visits: PageVisit[] = [
      createVisit({ pageVisitId: "a", domain: "github.com", durationSeconds: 120, classification: "aligned" }),
      createVisit({ pageVisitId: "b", domain: "docs.microsoft.com", durationSeconds: 60, classification: "aligned" })
    ];

    const summary = calculateSessionSummary(
      "Focused work",
      "2026-03-14T10:00:00.000Z",
      "2026-03-14T10:05:00.000Z",
      visits
    );

    expect(summary.focusPercentage).toBe(100);
    expect(summary.alignedSeconds).toBe(180);
    expect(summary.distractingSeconds).toBe(0);
    expect(summary.distractionCount).toBe(0);
  });

  it("returns 0% focus when all visits are distracting", () => {
    const visits: PageVisit[] = [
      createVisit({ pageVisitId: "a", domain: "youtube.com", durationSeconds: 60, classification: "distracting" }),
      createVisit({ pageVisitId: "b", domain: "twitter.com", durationSeconds: 30, classification: "distracting" })
    ];

    const summary = calculateSessionSummary(
      "Distracted session",
      "2026-03-14T10:00:00.000Z",
      "2026-03-14T10:05:00.000Z",
      visits
    );

    expect(summary.focusPercentage).toBe(0);
    expect(summary.alignedSeconds).toBe(0);
    expect(summary.distractingSeconds).toBe(90);
    expect(summary.distractionCount).toBe(1);
  });

  it("excludes zero-duration visits from domain aggregation", () => {
    const visits: PageVisit[] = [
      createVisit({ pageVisitId: "a", domain: "github.com", durationSeconds: 60, classification: "aligned" }),
      createVisit({ pageVisitId: "b", domain: "zero.com", durationSeconds: 0, classification: "aligned" }),
      createVisit({ pageVisitId: "c", domain: "youtube.com", durationSeconds: 0, classification: "distracting" })
    ];

    const summary = calculateSessionSummary(
      "Test zero duration",
      "2026-03-14T10:00:00.000Z",
      "2026-03-14T10:05:00.000Z",
      visits
    );

    expect(summary.topAlignedDomains.length).toBe(1);
    expect(summary.topAlignedDomains[0].domain).toBe("github.com");
    expect(summary.topDistractionDomains.length).toBe(0);
  });

  it("subtracts paused time from totalSessionSeconds", () => {
    const visits: PageVisit[] = [
      createVisit({ pageVisitId: "a", durationSeconds: 120, classification: "aligned" })
    ];

    const summary = calculateSessionSummary(
      "Paused session",
      "2026-03-14T10:00:00.000Z",
      "2026-03-14T10:10:00.000Z",
      visits,
      180
    );

    expect(summary.totalSessionSeconds).toBe(420);
  });

  it("limits topDistractionDomains to 5 entries", () => {
    const visits: PageVisit[] = [
      createVisit({ pageVisitId: "a", domain: "site1.com", durationSeconds: 10, classification: "distracting" }),
      createVisit({ pageVisitId: "b", domain: "site2.com", durationSeconds: 20, classification: "distracting" }),
      createVisit({ pageVisitId: "c", domain: "site3.com", durationSeconds: 30, classification: "distracting" }),
      createVisit({ pageVisitId: "d", domain: "site4.com", durationSeconds: 40, classification: "distracting" }),
      createVisit({ pageVisitId: "e", domain: "site5.com", durationSeconds: 50, classification: "distracting" }),
      createVisit({ pageVisitId: "f", domain: "site6.com", durationSeconds: 60, classification: "distracting" }),
      createVisit({ pageVisitId: "g", domain: "site7.com", durationSeconds: 70, classification: "distracting" })
    ];

    const summary = calculateSessionSummary(
      "Many distractions",
      "2026-03-14T10:00:00.000Z",
      "2026-03-14T10:10:00.000Z",
      visits
    );

    expect(summary.topDistractionDomains.length).toBe(5);
  });

  it("sorts domains by totalSeconds descending, then visitCount, then name", () => {
    const visits: PageVisit[] = [
      createVisit({ pageVisitId: "a", domain: "alpha.com", durationSeconds: 30, classification: "distracting" }),
      createVisit({ pageVisitId: "b", domain: "beta.com", durationSeconds: 30, classification: "distracting" }),
      createVisit({ pageVisitId: "c", domain: "beta.com", durationSeconds: 30, classification: "distracting" }),
      createVisit({ pageVisitId: "d", domain: "gamma.com", durationSeconds: 100, classification: "distracting" })
    ];

    const summary = calculateSessionSummary(
      "Domain sorting test",
      "2026-03-14T10:00:00.000Z",
      "2026-03-14T10:10:00.000Z",
      visits
    );

    expect(summary.topDistractionDomains[0].domain).toBe("gamma.com");
    expect(summary.topDistractionDomains[1].domain).toBe("beta.com");
    expect(summary.topDistractionDomains[2].domain).toBe("alpha.com");
  });

  it("counts distraction when session starts with distracting visit", () => {
    const visits: PageVisit[] = [
      createVisit({ pageVisitId: "a", durationSeconds: 30, classification: "distracting" }),
      createVisit({ pageVisitId: "b", durationSeconds: 60, classification: "aligned" }),
      createVisit({ pageVisitId: "c", durationSeconds: 20, classification: "distracting" })
    ];

    const summary = calculateSessionSummary(
      "Start distracted",
      "2026-03-14T10:00:00.000Z",
      "2026-03-14T10:05:00.000Z",
      visits
    );

    expect(summary.distractionCount).toBe(2);
  });
});

describe("calculateLiveSummary", () => {
  it("includes current classified visit in calculations", () => {
    const session = createSession({
      startedAt: "2026-03-14T10:00:00.000Z",
      visits: [
        createVisit({ pageVisitId: "a", durationSeconds: 60, classification: "aligned" })
      ],
      currentVisit: createInProgressVisit({
        enteredAt: "2026-03-14T10:01:00.000Z",
        visitState: "classified",
        classification: "distracting",
        domain: "youtube.com"
      })
    });

    const summary = calculateLiveSummary(session);

    expect(summary.distractingSeconds).toBeGreaterThan(0);
    expect(summary.distractionCount).toBe(1);
  });

  it("ignores current visit if not classified", () => {
    const session = createSession({
      startedAt: "2026-03-14T10:00:00.000Z",
      visits: [
        createVisit({ pageVisitId: "a", durationSeconds: 60, classification: "aligned" })
      ],
      currentVisit: createInProgressVisit({
        visitState: "classifying",
        classification: undefined
      })
    });

    const summary = calculateLiveSummary(session);

    expect(summary.alignedSeconds).toBe(60);
    expect(summary.totalTrackedSeconds).toBe(60);
    expect(summary.focusPercentage).toBe(100);
  });

  it("ignores current visit if in error state", () => {
    const session = createSession({
      startedAt: "2026-03-14T10:00:00.000Z",
      visits: [
        createVisit({ pageVisitId: "a", durationSeconds: 120, classification: "aligned" })
      ],
      currentVisit: createInProgressVisit({
        visitState: "error",
        classification: "distracting"
      })
    });

    const summary = calculateLiveSummary(session);

    expect(summary.alignedSeconds).toBe(120);
    expect(summary.distractingSeconds).toBe(0);
  });

  it("uses pausedAt as effective end when session is paused", () => {
    const session = createSession({
      startedAt: "2026-03-14T10:00:00.000Z",
      pausedAt: "2026-03-14T10:05:00.000Z",
      visits: [],
      currentVisit: createInProgressVisit({
        enteredAt: "2026-03-14T10:00:00.000Z",
        visitState: "classified",
        classification: "aligned"
      })
    });

    const summary = calculateLiveSummary(session);

    expect(summary.alignedSeconds).toBe(300);
  });

  it("uses endedAt as effective end for completed sessions", () => {
    const session = createSession({
      startedAt: "2026-03-14T10:00:00.000Z",
      endedAt: "2026-03-14T10:10:00.000Z",
      visits: [
        createVisit({ pageVisitId: "a", durationSeconds: 300, classification: "aligned" })
      ]
    });

    const summary = calculateLiveSummary(session);

    expect(summary.totalSessionSeconds).toBe(600);
    expect(summary.alignedSeconds).toBe(300);
  });

  it("includes totalPausedSeconds in summary calculation", () => {
    const session = createSession({
      startedAt: "2026-03-14T10:00:00.000Z",
      endedAt: "2026-03-14T10:10:00.000Z",
      totalPausedSeconds: 120,
      visits: [
        createVisit({ pageVisitId: "a", durationSeconds: 300, classification: "aligned" })
      ]
    });

    const summary = calculateLiveSummary(session);

    expect(summary.totalSessionSeconds).toBe(480);
  });

  it("handles session with no visits and no current visit", () => {
    const session = createSession({
      startedAt: "2026-03-14T10:00:00.000Z",
      visits: []
    });

    const summary = calculateLiveSummary(session);

    expect(summary.totalTrackedSeconds).toBe(0);
    expect(summary.focusPercentage).toBe(0);
    expect(summary.distractionCount).toBe(0);
  });

  it("combines completed visits with current visit correctly", () => {
    const session = createSession({
      startedAt: "2026-03-14T10:00:00.000Z",
      pausedAt: "2026-03-14T10:05:00.000Z",
      visits: [
        createVisit({ pageVisitId: "a", durationSeconds: 60, classification: "aligned", domain: "github.com" }),
        createVisit({ pageVisitId: "b", durationSeconds: 30, classification: "distracting", domain: "youtube.com" })
      ],
      currentVisit: createInProgressVisit({
        enteredAt: "2026-03-14T10:04:00.000Z",
        visitState: "classified",
        classification: "aligned",
        domain: "docs.microsoft.com"
      })
    });

    const summary = calculateLiveSummary(session);

    expect(summary.alignedSeconds).toBe(120);
    expect(summary.distractingSeconds).toBe(30);
    expect(summary.topAlignedDomains.length).toBe(2);
  });

  it("treats pausedBy idle and user the same for summary (metrics ignore pausedBy)", () => {
    const base = {
      startedAt: "2026-03-14T10:00:00.000Z",
      pausedAt: "2026-03-14T10:05:00.000Z",
      visits: [createVisit({ pageVisitId: "a", durationSeconds: 180, classification: "aligned" })],
      currentVisit: createInProgressVisit({
        enteredAt: "2026-03-14T10:03:00.000Z",
        visitState: "classified",
        classification: "aligned"
      })
    };
    const summaryUser = calculateLiveSummary(createSession({ ...base, pausedBy: "user" }));
    const summaryIdle = calculateLiveSummary(createSession({ ...base, pausedBy: "idle" }));

    expect(summaryIdle.totalSessionSeconds).toBe(summaryUser.totalSessionSeconds);
    expect(summaryIdle.totalTrackedSeconds).toBe(summaryUser.totalTrackedSeconds);
    expect(summaryIdle.alignedSeconds).toBe(summaryUser.alignedSeconds);
  });
});

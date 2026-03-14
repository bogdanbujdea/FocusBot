import { describe, expect, it } from "vitest";
import { calculateSessionSummary } from "../src/shared/metrics";
import type { PageVisit } from "../src/shared/types";

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
});

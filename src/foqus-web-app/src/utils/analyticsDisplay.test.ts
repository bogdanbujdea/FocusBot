import { describe, expect, it } from "vitest";
import type { TrendDataPoint } from "../api/types";
import {
  averageDistractionDurationSeconds,
  averageDistractionsPerSession,
  computeEndedSessionActiveSeconds,
  computeLiveSessionActiveSeconds,
  focusScoreTone,
  focusScoreToneClass,
  mapTrendDataPointForChart,
  secondsToChartMinutes,
} from "./analyticsDisplay";

describe("focusScoreTone", () => {
  it("classifies thresholds", () => {
    expect(focusScoreTone(null)).toBeNull();
    expect(focusScoreTone(undefined)).toBeNull();
    expect(focusScoreTone(85)).toBe("high");
    expect(focusScoreTone(70)).toBe("high");
    expect(focusScoreTone(69)).toBe("mid");
    expect(focusScoreTone(40)).toBe("mid");
    expect(focusScoreTone(39)).toBe("low");
  });
});

describe("focusScoreToneClass", () => {
  it("maps to CSS class names", () => {
    expect(focusScoreToneClass(80)).toBe("focus-pct-high");
    expect(focusScoreToneClass(50)).toBe("focus-pct-mid");
    expect(focusScoreToneClass(10)).toBe("focus-pct-low");
    expect(focusScoreToneClass(null)).toBe("");
  });
});

describe("computeEndedSessionActiveSeconds", () => {
  it("subtracts paused time from wall duration", () => {
    expect(
      computeEndedSessionActiveSeconds({
        startedAtUtc: "2025-01-15T10:00:00.000Z",
        endedAtUtc: "2025-01-15T10:30:00.000Z",
        totalPausedSeconds: 0,
      })
    ).toBe(30 * 60);
    expect(
      computeEndedSessionActiveSeconds({
        startedAtUtc: "2025-01-15T10:00:00.000Z",
        endedAtUtc: "2025-01-15T11:00:00.000Z",
        totalPausedSeconds: 600,
      })
    ).toBe(60 * 60 - 600);
  });

  it("returns 0 without end time", () => {
    expect(
      computeEndedSessionActiveSeconds({
        startedAtUtc: "2025-01-15T10:00:00.000Z",
        endedAtUtc: undefined,
        totalPausedSeconds: 0,
      })
    ).toBe(0);
  });

  it("handles very short sessions", () => {
    expect(
      computeEndedSessionActiveSeconds({
        startedAtUtc: "2025-01-15T10:00:00.000Z",
        endedAtUtc: "2025-01-15T10:00:28.000Z",
        totalPausedSeconds: 0,
      })
    ).toBe(28);
  });
});

describe("secondsToChartMinutes / mapTrendDataPointForChart", () => {
  it("rounds like AnalyticsPage", () => {
    expect(secondsToChartMinutes(150)).toBe(3);
    expect(secondsToChartMinutes(30)).toBe(1);
  });

  it("maps trend points for charts", () => {
    const dp: TrendDataPoint = {
      date: "2025-01-01",
      sessions: 1,
      focusedSeconds: 150,
      distractedSeconds: 30,
      focusScorePercent: 80,
      distractionCount: 1,
    };
    const row = mapTrendDataPointForChart(dp);
    expect(row.focusedMinutes).toBe(3);
    expect(row.distractedMinutes).toBe(1);
    expect(row.date).toBe("2025-01-01");
  });
});

describe("averageDistractionDurationSeconds", () => {
  it("returns null when count is zero", () => {
    expect(averageDistractionDurationSeconds(100, 0)).toBeNull();
  });

  it("divides distracted seconds by count", () => {
    expect(averageDistractionDurationSeconds(90, 3)).toBe(30);
  });
});

describe("averageDistractionsPerSession", () => {
  it("returns null without sessions", () => {
    expect(averageDistractionsPerSession(5, 0)).toBeNull();
  });

  it("divides count by sessions", () => {
    expect(averageDistractionsPerSession(10, 4)).toBe(2.5);
  });
});

describe("computeLiveSessionActiveSeconds", () => {
  it("subtracts total paused seconds from wall time to reference", () => {
    const now = new Date("2025-01-15T12:05:00.000Z").getTime();
    const started = "2025-01-15T12:00:00.000Z";
    expect(
      computeLiveSessionActiveSeconds(started, undefined, 60, now)
    ).toBe(5 * 60 - 60);
  });

  it("uses pausedAtUtc as end reference when paused", () => {
    const now = new Date("2025-01-15T12:10:00.000Z").getTime();
    const started = "2025-01-15T12:00:00.000Z";
    const paused = "2025-01-15T12:05:00.000Z";
    expect(
      computeLiveSessionActiveSeconds(started, paused, 0, now)
    ).toBe(5 * 60);
  });

  it("treats null pausedAtUtc as not paused", () => {
    const now = new Date("2025-01-15T12:10:00.000Z").getTime();
    const started = "2025-01-15T12:00:00.000Z";
    expect(computeLiveSessionActiveSeconds(started, null, 0, now)).toBe(10 * 60);
  });
});

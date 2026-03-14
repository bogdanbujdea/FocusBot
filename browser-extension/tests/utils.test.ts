import { describe, expect, it } from "vitest";
import {
  clampPercent,
  endOfDayLocal,
  formatSeconds,
  secondsBetween,
  startOfDayLocal,
  toDayKeyLocal
} from "../src/shared/utils";

describe("secondsBetween", () => {
  it("returns correct seconds between two timestamps", () => {
    const start = "2026-03-14T10:00:00.000Z";
    const end = "2026-03-14T10:05:30.000Z";

    expect(secondsBetween(start, end)).toBe(330);
  });

  it("returns 0 when end is before start (clamped)", () => {
    const start = "2026-03-14T10:05:00.000Z";
    const end = "2026-03-14T10:00:00.000Z";

    expect(secondsBetween(start, end)).toBe(0);
  });

  it("returns 0 for same timestamp", () => {
    const timestamp = "2026-03-14T10:00:00.000Z";

    expect(secondsBetween(timestamp, timestamp)).toBe(0);
  });

  it("rounds sub-second precision", () => {
    const start = "2026-03-14T10:00:00.000Z";
    const end = "2026-03-14T10:00:01.499Z";

    expect(secondsBetween(start, end)).toBe(1);
  });

  it("rounds up when milliseconds are 500 or more", () => {
    const start = "2026-03-14T10:00:00.000Z";
    const end = "2026-03-14T10:00:01.500Z";

    expect(secondsBetween(start, end)).toBe(2);
  });

  it("handles large time differences", () => {
    const start = "2026-03-14T00:00:00.000Z";
    const end = "2026-03-15T00:00:00.000Z";

    expect(secondsBetween(start, end)).toBe(86400);
  });
});

describe("clampPercent", () => {
  it("passes through valid percentages unchanged", () => {
    expect(clampPercent(50)).toBe(50);
    expect(clampPercent(0)).toBe(0);
    expect(clampPercent(100)).toBe(100);
    expect(clampPercent(73.5)).toBe(73.5);
  });

  it("clamps values above 100", () => {
    expect(clampPercent(150)).toBe(100);
    expect(clampPercent(100.1)).toBe(100);
  });

  it("clamps negative values to 0", () => {
    expect(clampPercent(-10)).toBe(0);
    expect(clampPercent(-0.1)).toBe(0);
  });

  it("returns 0 for NaN", () => {
    expect(clampPercent(NaN)).toBe(0);
  });

  it("returns 0 for Infinity", () => {
    expect(clampPercent(Infinity)).toBe(0);
    expect(clampPercent(-Infinity)).toBe(0);
  });
});

describe("formatSeconds", () => {
  it("formats seconds only when less than 60", () => {
    expect(formatSeconds(45)).toBe("45s");
    expect(formatSeconds(1)).toBe("1s");
    expect(formatSeconds(59)).toBe("59s");
  });

  it("formats minutes and seconds", () => {
    expect(formatSeconds(60)).toBe("1m 0s");
    expect(formatSeconds(90)).toBe("1m 30s");
    expect(formatSeconds(125)).toBe("2m 5s");
  });

  it("formats hours and minutes", () => {
    expect(formatSeconds(3600)).toBe("1h 0m");
    expect(formatSeconds(3660)).toBe("1h 1m");
    expect(formatSeconds(7320)).toBe("2h 2m");
  });

  it("handles zero seconds", () => {
    expect(formatSeconds(0)).toBe("0s");
  });

  it("clamps negative input to 0", () => {
    expect(formatSeconds(-10)).toBe("0s");
    expect(formatSeconds(-100)).toBe("0s");
  });

  it("rounds fractional seconds", () => {
    expect(formatSeconds(45.4)).toBe("45s");
    expect(formatSeconds(45.6)).toBe("46s");
  });
});

describe("toDayKeyLocal", () => {
  it("returns YYYY-MM-DD format", () => {
    const date = new Date(2026, 2, 14, 15, 30, 0);
    const result = toDayKeyLocal(date.toISOString());

    expect(result).toBe("2026-03-14");
  });

  it("pads single-digit month and day with zeros", () => {
    const date = new Date(2026, 0, 5, 10, 0, 0);
    const result = toDayKeyLocal(date.toISOString());

    expect(result).toBe("2026-01-05");
  });

  it("handles end of year dates", () => {
    const date = new Date(2026, 11, 31, 23, 59, 59);
    const result = toDayKeyLocal(date.toISOString());

    expect(result).toBe("2026-12-31");
  });
});

describe("startOfDayLocal", () => {
  it("returns midnight (00:00:00.000) for the given date", () => {
    const input = new Date(2026, 2, 14, 15, 30, 45, 123);
    const result = startOfDayLocal(input);

    expect(result.getFullYear()).toBe(2026);
    expect(result.getMonth()).toBe(2);
    expect(result.getDate()).toBe(14);
    expect(result.getHours()).toBe(0);
    expect(result.getMinutes()).toBe(0);
    expect(result.getSeconds()).toBe(0);
    expect(result.getMilliseconds()).toBe(0);
  });

  it("preserves the original date object", () => {
    const input = new Date(2026, 2, 14, 15, 30, 45, 123);
    startOfDayLocal(input);

    expect(input.getHours()).toBe(15);
  });
});

describe("endOfDayLocal", () => {
  it("returns 23:59:59.999 for the given date", () => {
    const input = new Date(2026, 2, 14, 10, 0, 0, 0);
    const result = endOfDayLocal(input);

    expect(result.getFullYear()).toBe(2026);
    expect(result.getMonth()).toBe(2);
    expect(result.getDate()).toBe(14);
    expect(result.getHours()).toBe(23);
    expect(result.getMinutes()).toBe(59);
    expect(result.getSeconds()).toBe(59);
    expect(result.getMilliseconds()).toBe(999);
  });

  it("preserves the original date object", () => {
    const input = new Date(2026, 2, 14, 10, 0, 0, 0);
    endOfDayLocal(input);

    expect(input.getHours()).toBe(10);
  });
});

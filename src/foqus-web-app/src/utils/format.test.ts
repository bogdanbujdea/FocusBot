import { describe, expect, it } from "vitest";
import {
  daysAgo,
  formatDate,
  formatDateTime,
  formatDuration,
  formatDurationSeconds,
  startOfLocalDayIso,
} from "./format";

describe("formatDuration", () => {
  it("formats edge cases", () => {
    expect(formatDuration(0)).toBe("0s");
    expect(formatDuration(45)).toBe("45s");
    expect(formatDuration(60)).toBe("1m");
    expect(formatDuration(90)).toBe("1m");
    expect(formatDuration(3661)).toBe("1h 1m");
    expect(formatDuration(7200)).toBe("2h 0m");
  });
});

describe("formatDurationSeconds", () => {
  it("includes seconds when under one hour", () => {
    expect(formatDurationSeconds(240)).toBe("4m 0s");
    expect(formatDurationSeconds(28)).toBe("28s");
    expect(formatDurationSeconds(3661)).toBe("1h 1m");
  });
});

describe("daysAgo", () => {
  it("returns ISO strings in the past", () => {
    const one = new Date(daysAgo(1)).getTime();
    const seven = new Date(daysAgo(7)).getTime();
    const now = Date.now();
    expect(now - one).toBeGreaterThan(23 * 60 * 60 * 1000);
    expect(now - one).toBeLessThan(25 * 60 * 60 * 1000);
    expect(now - seven).toBeGreaterThan(6 * 24 * 60 * 60 * 1000);
    expect(now - seven).toBeLessThan(8 * 24 * 60 * 60 * 1000);
  });
});

describe("startOfLocalDayIso", () => {
  it("is at local midnight", () => {
    const iso = startOfLocalDayIso();
    const d = new Date(iso);
    expect(d.getHours()).toBe(0);
    expect(d.getMinutes()).toBe(0);
    expect(d.getSeconds()).toBe(0);
    expect(d.getMilliseconds()).toBe(0);
  });
});

describe("formatDate and formatDateTime", () => {
  it("produce non-empty strings", () => {
    const s = "2025-01-15T15:30:00.000Z";
    expect(formatDate(s).length).toBeGreaterThan(4);
    expect(formatDateTime(s).length).toBeGreaterThan(4);
  });
});

import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { SessionTimer } from "./SessionTimer";

describe("SessionTimer", () => {
  it("shows active seconds minus total paused (5m wall, 1m paused => 4m 0s)", () => {
    const now = new Date("2025-06-01T12:05:00.000Z").getTime();
    const started = "2025-06-01T12:00:00.000Z";
    render(
      <SessionTimer
        startedAtUtc={started}
        totalPausedSeconds={60}
        nowMs={() => now}
      />
    );
    expect(screen.getByTestId("session-timer")).toHaveTextContent("4m 0s");
  });

  it("freezes using pausedAtUtc (ignores later now)", () => {
    const now = new Date("2025-06-01T12:30:00.000Z").getTime();
    const started = "2025-06-01T12:00:00.000Z";
    const paused = "2025-06-01T12:05:00.000Z";
    render(
      <SessionTimer
        startedAtUtc={started}
        pausedAtUtc={paused}
        totalPausedSeconds={0}
        nowMs={() => now}
      />
    );
    expect(screen.getByTestId("session-timer")).toHaveTextContent("5m 0s");
  });
});

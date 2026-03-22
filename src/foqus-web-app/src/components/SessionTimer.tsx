import { startTransition, useEffect, useState } from "react";
import { computeLiveSessionActiveSeconds } from "../utils/analyticsDisplay";
import { formatDurationSeconds } from "../utils/format";
import "./shared.css";

export interface SessionTimerProps {
  startedAtUtc: string;
  pausedAtUtc?: string;
  totalPausedSeconds: number;
  /** Fixed clock for tests; when set, no live interval runs. */
  nowMs?: () => number;
}

/**
 * Live elapsed active time (excludes recorded paused seconds; freezes when paused).
 */
export function SessionTimer({
  startedAtUtc,
  pausedAtUtc,
  totalPausedSeconds,
  nowMs,
}: SessionTimerProps) {
  const [displayNow, setDisplayNow] = useState(() =>
    nowMs ? nowMs() : Date.now()
  );

  useEffect(() => {
    if (nowMs) {
      startTransition(() => setDisplayNow(nowMs()));
      return;
    }
    if (pausedAtUtc) {
      startTransition(() => setDisplayNow(Date.now()));
      return;
    }
    startTransition(() => setDisplayNow(Date.now()));
    const id = window.setInterval(() => setDisplayNow(Date.now()), 1000);
    return () => window.clearInterval(id);
  }, [nowMs, pausedAtUtc, startedAtUtc, totalPausedSeconds]);

  const seconds = computeLiveSessionActiveSeconds(
    startedAtUtc,
    pausedAtUtc,
    totalPausedSeconds,
    displayNow
  );

  return (
    <span className="session-timer" data-testid="session-timer">
      {formatDurationSeconds(seconds)}
    </span>
  );
}

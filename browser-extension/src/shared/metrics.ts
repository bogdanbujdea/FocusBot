import type { DomainAggregate, FocusSession, PageVisit, SessionSummary } from "./types";
import { clampPercent, secondsBetween } from "./utils";

const aggregateDomains = (
  visits: PageVisit[],
  targetClassification: "aligned" | "distracting"
): DomainAggregate[] => {
  const bucket = new Map<string, DomainAggregate>();

  for (const visit of visits) {
    if (visit.classification !== targetClassification || visit.durationSeconds <= 0) {
      continue;
    }

    const current = bucket.get(visit.domain) ?? {
      domain: visit.domain,
      totalSeconds: 0,
      visitCount: 0
    };
    current.totalSeconds += visit.durationSeconds;
    current.visitCount += 1;
    bucket.set(visit.domain, current);
  }

  return [...bucket.values()]
    .sort((left, right) => {
      if (right.totalSeconds !== left.totalSeconds) {
        return right.totalSeconds - left.totalSeconds;
      }

      if (right.visitCount !== left.visitCount) {
        return right.visitCount - left.visitCount;
      }

      return left.domain.localeCompare(right.domain);
    })
    .slice(0, 5);
};

export const calculateSessionSummary = (
  taskName: string,
  startedAt: string,
  endedAt: string,
  visits: PageVisit[]
): SessionSummary => {
  const alignedSeconds = visits
    .filter((visit) => visit.classification === "aligned")
    .reduce((sum, visit) => sum + visit.durationSeconds, 0);
  const distractingSeconds = visits
    .filter((visit) => visit.classification === "distracting")
    .reduce((sum, visit) => sum + visit.durationSeconds, 0);
  const totalTrackedSeconds = alignedSeconds + distractingSeconds;

  let distractionCount = 0;
  let previousClassification: "aligned" | "distracting" | undefined;

  for (const visit of visits) {
    if (
      visit.classification === "distracting" &&
      (previousClassification === undefined || previousClassification === "aligned")
    ) {
      distractionCount += 1;
    }
    previousClassification = visit.classification;
  }

  return {
    taskName,
    totalSessionSeconds: secondsBetween(startedAt, endedAt),
    totalTrackedSeconds,
    alignedSeconds,
    distractingSeconds,
    distractionCount,
    focusPercentage: clampPercent(totalTrackedSeconds === 0 ? 0 : (alignedSeconds / totalTrackedSeconds) * 100),
    contextSwitchCostSeconds:
      distractionCount === 0 ? 0 : Math.round(distractingSeconds / Math.max(1, distractionCount)),
    topDistractionDomains: aggregateDomains(visits, "distracting"),
    topAlignedDomains: aggregateDomains(visits, "aligned")
  };
};

/**
 * Builds a live SessionSummary from an active session by including completed visits
 * plus the current in-progress visit (if classified) with duration up to "now".
 * Use this to display real-time metrics while a session is running.
 */
export const calculateLiveSummary = (session: FocusSession): SessionSummary => {
  const effectiveEnd = session.endedAt ?? new Date().toISOString();
  const effectiveVisits: PageVisit[] = [...session.visits];

  const cv = session.currentVisit;
  if (cv?.visitState === "classified" && cv.classification && cv.enteredAt) {
    const durationSeconds = secondsBetween(cv.enteredAt, effectiveEnd);
    effectiveVisits.push({
      pageVisitId: cv.visitToken,
      sessionId: session.sessionId,
      tabId: cv.tabId,
      url: cv.url,
      domain: cv.domain,
      title: cv.title,
      enteredAt: cv.enteredAt,
      leftAt: effectiveEnd,
      durationSeconds,
      classification: cv.classification,
      confidence: cv.confidence ?? 0,
      reason: cv.reason
    });
  }

  return calculateSessionSummary(session.taskText, session.startedAt, effectiveEnd, effectiveVisits);
};

export const stripActiveSessionForHistory = (session: FocusSession): FocusSession => ({
  ...session,
  currentVisit: undefined
});

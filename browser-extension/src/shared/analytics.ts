import type { AnalyticsResponse, DailyStats, DateRange, DomainAggregate, FocusSession } from "./types";
import { clampPercent, endOfDayLocal, startOfDayLocal, toDayKeyLocal } from "./utils";

const rangeToDays = (range: DateRange): number => {
  if (range === "today") {
    return 1;
  }
  if (range === "7d") {
    return 7;
  }
  return 30;
};

const buildDateList = (days: number): string[] => {
  const dates: string[] = [];
  const now = new Date();

  for (let offset = days - 1; offset >= 0; offset -= 1) {
    const point = new Date(now.getFullYear(), now.getMonth(), now.getDate() - offset);
    dates.push(toDayKeyLocal(point.toISOString()));
  }

  return dates;
};

const aggregateDomains = (
  sessions: FocusSession[],
  targetClassification: "aligned" | "distracting"
): DomainAggregate[] => {
  const bucket = new Map<string, DomainAggregate>();

  for (const session of sessions) {
    for (const visit of session.visits) {
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

const createEmptyDay = (day: string): DailyStats => ({
  date: day,
  totalSessions: 0,
  totalTrackedSeconds: 0,
  totalAlignedSeconds: 0,
  totalDistractingSeconds: 0,
  distractionCount: 0,
  averageContextSwitchCostSeconds: 0,
  focusPercentage: 0,
  mostCommonDistractingDomains: [],
  mostCommonAlignedDomains: []
});

export const calculateAnalytics = (range: DateRange, history: FocusSession[]): AnalyticsResponse => {
  const days = rangeToDays(range);
  const keys = buildDateList(days);
  const fromDate = startOfDayLocal(new Date(keys[0]));
  const toDate = endOfDayLocal(new Date(keys[keys.length - 1]));
  const byDay = new Map<string, DailyStats>(keys.map((key) => [key, createEmptyDay(key)]));
  const selectedSessions = history.filter((session) => {
    if (!session.endedAt || !session.summary) {
      return false;
    }

    const endedAt = new Date(session.endedAt);
    return endedAt >= fromDate && endedAt <= toDate;
  });

  for (const session of selectedSessions) {
    if (!session.endedAt || !session.summary) {
      continue;
    }

    const dayKey = toDayKeyLocal(session.endedAt);
    const daily = byDay.get(dayKey);
    if (!daily) {
      continue;
    }

    daily.totalSessions += 1;
    daily.totalTrackedSeconds += session.summary.totalTrackedSeconds;
    daily.totalAlignedSeconds += session.summary.alignedSeconds;
    daily.totalDistractingSeconds += session.summary.distractingSeconds;
    daily.distractionCount += session.summary.distractionCount;
  }

  for (const day of byDay.values()) {
    day.focusPercentage = clampPercent(
      day.totalTrackedSeconds === 0 ? 0 : (day.totalAlignedSeconds / day.totalTrackedSeconds) * 100
    );
    day.averageContextSwitchCostSeconds =
      day.distractionCount === 0 ? 0 : Math.round(day.totalDistractingSeconds / day.distractionCount);

    const daySessions = selectedSessions.filter(
      (session) => session.endedAt && toDayKeyLocal(session.endedAt) === day.date
    );
    day.mostCommonDistractingDomains = aggregateDomains(daySessions, "distracting");
    day.mostCommonAlignedDomains = aggregateDomains(daySessions, "aligned");
  }

  const statsByDay = keys.map((key) => byDay.get(key) ?? createEmptyDay(key));
  const totals = statsByDay.reduce<Omit<DailyStats, "date">>(
    (accumulator, day) => {
      accumulator.totalSessions += day.totalSessions;
      accumulator.totalTrackedSeconds += day.totalTrackedSeconds;
      accumulator.totalAlignedSeconds += day.totalAlignedSeconds;
      accumulator.totalDistractingSeconds += day.totalDistractingSeconds;
      accumulator.distractionCount += day.distractionCount;
      return accumulator;
    },
    {
      totalSessions: 0,
      totalTrackedSeconds: 0,
      totalAlignedSeconds: 0,
      totalDistractingSeconds: 0,
      distractionCount: 0,
      averageContextSwitchCostSeconds: 0,
      focusPercentage: 0,
      mostCommonDistractingDomains: [],
      mostCommonAlignedDomains: []
    }
  );

  totals.focusPercentage = clampPercent(
    totals.totalTrackedSeconds === 0 ? 0 : (totals.totalAlignedSeconds / totals.totalTrackedSeconds) * 100
  );
  totals.averageContextSwitchCostSeconds =
    totals.distractionCount === 0 ? 0 : Math.round(totals.totalDistractingSeconds / totals.distractionCount);
  totals.mostCommonDistractingDomains = aggregateDomains(selectedSessions, "distracting");
  totals.mostCommonAlignedDomains = aggregateDomains(selectedSessions, "aligned");

  return {
    range,
    from: fromDate.toISOString(),
    to: toDate.toISOString(),
    statsByDay,
    totals,
    recentSessions: selectedSessions
      .sort((left, right) => Date.parse(right.startedAt) - Date.parse(left.startedAt))
      .slice(0, 10)
  };
};

import type {
  AnalyticsInsights,
  AnalyticsResponse,
  CompletedSession,
  DailyStats,
  DateRange,
  DomainAggregate
} from "./types";
import { clampPercent, endOfDayLocal, startOfDayLocal, toDayKeyLocal } from "./utils";

const rangeToDays = (range: DateRange): number => {
  if (range === "today") {
    return 1;
  }
  if (range === "7d") {
    return 7;
  }
  if (range === "30d") {
    return 30;
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

const aggregateDomainsFromSummaries = (
  sessions: CompletedSession[],
  targetType: "aligned" | "distracting"
): DomainAggregate[] => {
  const bucket = new Map<string, DomainAggregate>();

  for (const session of sessions) {
    const domains =
      targetType === "aligned"
        ? session.summary.topAlignedDomains
        : session.summary.topDistractionDomains;

    for (const domain of domains) {
      const current =
        bucket.get(domain.domain) ?? {
          domain: domain.domain,
          totalSeconds: 0,
          visitCount: 0
        };
      current.totalSeconds += domain.totalSeconds;
      current.visitCount += domain.visitCount;
      bucket.set(domain.domain, current);
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

export const calculateAnalytics = (
  range: DateRange,
  history: CompletedSession[]
): AnalyticsResponse => {
  const now = new Date();
  let fromDate: Date;
  let toDate: Date;
  let keys: string[];

  if (range === "all") {
    if (history.length === 0) {
      keys = [toDayKeyLocal(now.toISOString())];
      fromDate = startOfDayLocal(now);
      toDate = endOfDayLocal(now);
    } else {
      const daySet = new Set<string>();
      for (const session of history) {
        daySet.add(toDayKeyLocal(session.endedAt));
      }
      keys = [...daySet].sort();
      fromDate = startOfDayLocal(new Date(keys[0]));
      toDate = endOfDayLocal(new Date(keys[keys.length - 1]));
    }
  } else {
    const days = rangeToDays(range);
    keys = buildDateList(days);
    fromDate = startOfDayLocal(new Date(keys[0]));
    toDate = endOfDayLocal(new Date(keys[keys.length - 1]));
  }

  const byDay = new Map<string, DailyStats>(keys.map((key) => [key, createEmptyDay(key)]));
  const selectedSessions = history.filter((session) => {
    const endedAt = new Date(session.endedAt);
    return endedAt >= fromDate && endedAt <= toDate;
  });

  for (const session of selectedSessions) {
    const dayKey = toDayKeyLocal(session.endedAt);
    let daily = byDay.get(dayKey);
    if (!daily) {
      daily = createEmptyDay(dayKey);
      byDay.set(dayKey, daily);
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
      (session) => toDayKeyLocal(session.endedAt) === day.date
    );
    day.mostCommonDistractingDomains = aggregateDomainsFromSummaries(daySessions, "distracting");
    day.mostCommonAlignedDomains = aggregateDomainsFromSummaries(daySessions, "aligned");
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
  totals.mostCommonDistractingDomains = aggregateDomainsFromSummaries(selectedSessions, "distracting");
  totals.mostCommonAlignedDomains = aggregateDomainsFromSummaries(selectedSessions, "aligned");

  const sessionsByDay: Record<string, CompletedSession[]> = {};
  for (const session of selectedSessions) {
    const dayKey = toDayKeyLocal(session.endedAt);
    if (!sessionsByDay[dayKey]) {
      sessionsByDay[dayKey] = [];
    }
    sessionsByDay[dayKey].push(session);
  }
  for (const dayKey of Object.keys(sessionsByDay)) {
    sessionsByDay[dayKey].sort((a, b) => Date.parse(b.startedAt) - Date.parse(a.startedAt));
  }

  const insights = calculateInsights(statsByDay, totals);

  return {
    range,
    from: fromDate.toISOString(),
    to: toDate.toISOString(),
    statsByDay,
    totals,
    recentSessions: selectedSessions
      .sort((left, right) => Date.parse(right.startedAt) - Date.parse(left.startedAt))
      .slice(0, 10),
    sessionsByDay,
    insights
  };
};

const calculateInsights = (
  statsByDay: DailyStats[],
  totals: Omit<DailyStats, "date">
): AnalyticsInsights => {
  const daysWithSessions = statsByDay.filter((day) => day.totalSessions > 0 && day.totalTrackedSeconds > 0);

  let bestFocusDay: AnalyticsInsights["bestFocusDay"] = null;
  if (daysWithSessions.length > 0) {
    const best = daysWithSessions.reduce((prev, curr) =>
      curr.focusPercentage > prev.focusPercentage ? curr : prev
    );
    bestFocusDay = { date: best.date, focusPercentage: best.focusPercentage };
  }

  const averageSessionLengthSeconds =
    totals.totalSessions === 0 ? 0 : Math.round(totals.totalTrackedSeconds / totals.totalSessions);

  return {
    bestFocusDay,
    averageSessionLengthSeconds
  };
};

export const APP_KEYS = {
  settings: "focusbot.settings",
  activeSession: "focusbot.activeSession",
  sessions: "focusbot.sessions",
  classificationCache: "focusbot.classificationCache",
  lastSummary: "focusbot.lastSummary"
} as const;

export const DEFAULT_MODEL = "gpt-4o-mini";

export const nowIso = (): string => new Date().toISOString();

export const createId = (): string => crypto.randomUUID();

export const secondsBetween = (startIso: string, endIso: string): number => {
  const deltaMs = Date.parse(endIso) - Date.parse(startIso);
  return Math.max(0, Math.round(deltaMs / 1000));
};

export const clampPercent = (value: number): number => {
  if (Number.isNaN(value) || !Number.isFinite(value)) {
    return 0;
  }

  return Math.max(0, Math.min(100, value));
};

export const toDayKeyLocal = (iso: string): string => {
  const date = new Date(iso);
  const year = date.getFullYear();
  const month = `${date.getMonth() + 1}`.padStart(2, "0");
  const day = `${date.getDate()}`.padStart(2, "0");
  return `${year}-${month}-${day}`;
};

export const startOfDayLocal = (date: Date): Date =>
  new Date(date.getFullYear(), date.getMonth(), date.getDate(), 0, 0, 0, 0);

export const endOfDayLocal = (date: Date): Date =>
  new Date(date.getFullYear(), date.getMonth(), date.getDate(), 23, 59, 59, 999);

export const formatSeconds = (seconds: number): string => {
  const safe = Math.max(0, Math.round(seconds));
  const hours = Math.floor(safe / 3600);
  const minutes = Math.floor((safe % 3600) / 60);
  const remainingSeconds = safe % 60;

  if (hours > 0) {
    return `${hours}h ${minutes}m`;
  }

  if (minutes > 0) {
    return `${minutes}m ${remainingSeconds}s`;
  }

  return `${remainingSeconds}s`;
};

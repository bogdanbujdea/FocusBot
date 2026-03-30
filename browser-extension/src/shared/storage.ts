import type { CompletedSession, FocusSession, PlanType, SessionSummary, Settings } from "./types";
import { APP_KEYS, DEFAULT_MODEL } from "./utils";

type ClassificationCacheMap = Record<string, CacheEntry>;

export interface CacheEntry {
  classification: "aligned" | "neutral" | "distracting";
  confidence: number;
  reason?: string;
  createdAt: string;
}

/** Pending cloud operation that should be retried when network is restored. */
export interface OfflineQueueItem {
  id: string;
  type: "session-start" | "session-end";
  payload: Record<string, unknown>;
  createdAt: string;
  retryCount: number;
}

const defaultSettings: Settings = {
  plan: "trial",
  openAiApiKey: "",
  classifierModel: DEFAULT_MODEL,
  onboardingCompleted: false
};

// Storage implementation uses chrome.storage.local for all data.
// API key is stored here and is only accessible by this extension
// (not by web pages or other extensions). It is not encrypted at rest,
// as the browser provides no DPAPI equivalent. This is the standard
// "as safe as the platform allows" approach for BYOK mode.
const getFromStorage = async <T>(key: string): Promise<T | null> => {
  const record = await chrome.storage.local.get(key);
  if (!(key in record)) {
    return null;
  }

  return record[key] as T;
};

const setInStorage = async (key: string, value: unknown): Promise<void> => {
  await chrome.storage.local.set({ [key]: value });
};

/**
 * Migrates legacy authMode values to the new PlanType. Old BYOK users become
 * trial, old foqus-account users become cloud-managed (closest equivalent).
 */
const migrateLegacyAuthMode = (stored: Record<string, unknown>): PlanType => {
  const legacy = (stored as { authMode?: string }).authMode;
  if (legacy === "foqus-account") return "cloud-managed";
  return "trial";
};

export const loadSettings = async (): Promise<Settings> => {
  const stored = await getFromStorage<Record<string, unknown>>(APP_KEYS.settings);
  if (!stored) {
    return defaultSettings;
  }

  const plan: PlanType =
    (stored.plan as PlanType | undefined) ??
    migrateLegacyAuthMode(stored);

  return {
    ...defaultSettings,
    ...stored,
    plan,
    focusbotEmail: stored.focusbotEmail as string | undefined
  };
};

export const saveSettings = async (settings: Settings): Promise<void> => {
  await setInStorage(APP_KEYS.settings, settings);
};

export const patchSettings = async (partial: Partial<Settings>): Promise<Settings> => {
  const current = await loadSettings();
  const next: Settings = {
    ...current,
    ...partial
  };

  await saveSettings(next);
  return next;
};

export const loadActiveSession = async (): Promise<FocusSession | null> =>
  getFromStorage<FocusSession>(APP_KEYS.activeSession);

export const saveActiveSession = async (session: FocusSession | null): Promise<void> => {
  if (!session) {
    await chrome.storage.local.remove(APP_KEYS.activeSession);
    return;
  }

  await setInStorage(APP_KEYS.activeSession, session);
};

export const loadSessions = async (): Promise<FocusSession[]> =>
  (await getFromStorage<FocusSession[]>(APP_KEYS.sessions)) ?? [];

export const saveSession = async (session: FocusSession): Promise<void> => {
  const current = await loadSessions();
  const filtered = current.filter((item) => item.sessionId !== session.sessionId);
  filtered.push(session);
  filtered.sort((left, right) => Date.parse(right.startedAt) - Date.parse(left.startedAt));
  await setInStorage(APP_KEYS.sessions, filtered);
};

export const loadCompletedSessions = async (): Promise<CompletedSession[]> =>
  (await getFromStorage<CompletedSession[]>(APP_KEYS.completedSessions)) ?? [];

export const saveCompletedSession = async (session: CompletedSession): Promise<void> => {
  const current = await loadCompletedSessions();
  const filtered = current.filter((item) => item.sessionId !== session.sessionId);
  filtered.push(session);
  filtered.sort((left, right) => Date.parse(right.endedAt) - Date.parse(left.endedAt));
  await setInStorage(APP_KEYS.completedSessions, filtered);
};

export const setCompletedSessions = async (sessions: CompletedSession[]): Promise<void> => {
  await setInStorage(APP_KEYS.completedSessions, sessions);
};

export const pruneOldSessions = async (
  maxCount: number,
  maxAgeDays: number
): Promise<void> => {
  const sessions = await loadCompletedSessions();
  const cutoffDate = new Date();
  cutoffDate.setDate(cutoffDate.getDate() - maxAgeDays);

  const filtered = sessions
    .filter((s) => new Date(s.endedAt) >= cutoffDate)
    .sort((a, b) => Date.parse(b.endedAt) - Date.parse(a.endedAt))
    .slice(0, maxCount);

  await setInStorage(APP_KEYS.completedSessions, filtered);
};

export const loadClassificationCache = async (): Promise<ClassificationCacheMap> =>
  (await getFromStorage<ClassificationCacheMap>(APP_KEYS.classificationCache)) ?? {};

export const saveClassificationCache = async (cache: ClassificationCacheMap): Promise<void> => {
  await setInStorage(APP_KEYS.classificationCache, cache);
};

export const loadLastSummary = async (): Promise<SessionSummary | null> =>
  getFromStorage<SessionSummary>(APP_KEYS.lastSummary);

export const saveLastSummary = async (summary: SessionSummary | null): Promise<void> => {
  if (!summary) {
    await chrome.storage.local.remove(APP_KEYS.lastSummary);
    return;
  }

  await setInStorage(APP_KEYS.lastSummary, summary);
};

export const loadLastError = async (): Promise<string | null> =>
  getFromStorage<string>(APP_KEYS.lastError);

export const saveLastError = async (errorMessage: string | null): Promise<void> => {
  if (!errorMessage) {
    await chrome.storage.local.remove(APP_KEYS.lastError);
    return;
  }

  await setInStorage(APP_KEYS.lastError, errorMessage);
};

export const loadOfflineQueue = async (): Promise<OfflineQueueItem[]> =>
  (await getFromStorage<OfflineQueueItem[]>(APP_KEYS.offlineQueue)) ?? [];

export const saveOfflineQueue = async (queue: OfflineQueueItem[]): Promise<void> => {
  await setInStorage(APP_KEYS.offlineQueue, queue);
};

export const enqueueOfflineItem = async (item: OfflineQueueItem): Promise<void> => {
  const current = await loadOfflineQueue();
  current.push(item);
  await saveOfflineQueue(current);
};

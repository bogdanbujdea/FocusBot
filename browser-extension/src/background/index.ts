import { calculateAnalytics } from "../shared/analytics";
import { classifyPage, mapApiScoreToClassification } from "../shared/classifier";
import { calculateLiveSummary, calculateSessionSummary, stripActiveSessionForHistory } from "../shared/metrics";
import {
  loadActiveSession,
  loadCompletedSessions,
  loadLastError,
  loadLastSummary,
  loadSessions,
  loadSettings,
  patchSettings,
  pruneOldSessions,
  saveActiveSession,
  saveCompletedSession,
  saveLastError,
  saveLastSummary,
  saveSession,
  setCompletedSessions,
  enqueueOfflineItem
} from "../shared/storage";
import type {
  ClassificationResult,
  CompletedSession,
  FocusSession,
  InProgressVisit,
  RuntimeRequest,
  RuntimeResponse
} from "../shared/types";
import { planRequiresApiKey } from "../shared/types";
import { APP_KEYS, createId, nowIso, secondsBetween, sleep } from "../shared/utils";
import { getDomain, isTrackableUrl } from "../shared/url";
import { ICON_DATA_URLS, type IconState } from "../shared/types";
import {
  loadFocusbotAuthSession,
  clearFocusbotAuthSession
} from "../shared/focusbotAuth";
import {
  connectFocusHub,
  disconnectFocusHub,
  type ClassificationChangedEvent,
  type SessionEndedEvent,
  type SessionPausedEvent,
  type SessionResumedEvent,
  type SessionStartedEvent
} from "../shared/signalr";
import {
  startCloudSession,
  endCloudSession,
  getActiveCloudSession,
  pauseCloudSession,
  resumeCloudSession,
  getSubscriptionStatus,
  deregisterClient,
  registerClient,
  getClientHostFromUserAgent,
  fetchCurrentUser
} from "../shared/apiClient";
import { mapBackendPlanType } from "../shared/subscription";
import type { IntegrationState } from "../shared/integrationTypes";
import { startExtensionPresence } from "../shared/extensionPresence";

// Chrome persists openPanelOnActionClick across extension reloads; force popup as the toolbar action.
void chrome.sidePanel.setPanelBehavior({ openPanelOnActionClick: false }).catch((error) => {
  console.warn("[Foqus] sidePanel.setPanelBehavior failed:", error);
});

const openSidePanelForAuthWindow = async (sender: chrome.runtime.MessageSender): Promise<void> => {
  let windowId = sender.tab?.windowId;
  if (windowId === undefined) {
    try {
      const last = await chrome.windows.getLastFocused({ windowTypes: ["normal"] });
      windowId = last.id;
    } catch {
      return;
    }
  }
  if (windowId === undefined) return;
  try {
    await chrome.sidePanel.open({ windowId });
  } catch (error) {
    console.warn("[Foqus] sidePanel.open after sign-in failed:", error);
  }
};

// ---------------------------------------------------------------------------
// Client helpers
// ---------------------------------------------------------------------------

const SIGNALR_RECONNECT_ALARM_NAME = "focusbot-signalr-reconnect";

/** True when a browser window is focused (vs another app). Used to avoid clobbering desktop visit state from background tab events. */
let browserWindowInForeground = true;

const getStaticIntegrationState = (): IntegrationState => ({
  connected: false,
  browserInForeground: browserWindowInForeground
});

const getStoredClientId = async (): Promise<string | null> => {
  const result = await chrome.storage.local.get([APP_KEYS.clientId, APP_KEYS.deviceId]);
  let id = (result[APP_KEYS.clientId] as string) ?? (result[APP_KEYS.deviceId] as string) ?? null;
  if (id && !result[APP_KEYS.clientId]) {
    await chrome.storage.local.set({ [APP_KEYS.clientId]: id });
  }
  return id;
};

const getStoredServerSessionId = async (): Promise<string | null> => {
  const result = await chrome.storage.local.get(APP_KEYS.serverSessionId);
  return (result[APP_KEYS.serverSessionId] as string) ?? null;
};

const resolveServerSessionIdForSync = async (localSessionId: string): Promise<string | null> => {
  const stored = await getStoredServerSessionId();
  if (stored) {
    return stored;
  }

  const auth = await loadFocusbotAuthSession();
  if (!auth?.accessToken) {
    return null;
  }

  const remote = await getActiveCloudSession();
  if (!remote?.id) {
    return null;
  }

  await chrome.storage.local.set({ [APP_KEYS.serverSessionId]: remote.id });
  if (remote.id !== localSessionId) {
    await saveActiveSession(buildLocalSessionFromServer(remote));
    await startBadgeInterval();
  }

  return remote.id;
};

const buildLocalSessionFromServer = (session: {
  id: string;
  sessionTitle: string;
  sessionContext?: string;
  startedAtUtc: string;
  pausedAtUtc?: string;
  totalPausedSeconds?: number;
  isPaused?: boolean;
}): FocusSession => {
  const local: FocusSession = {
    sessionId: session.id,
    taskText: session.sessionTitle,
    taskHints: session.sessionContext?.trim() || undefined,
    startedAt: session.startedAtUtc,
    visits: []
  };

  if (session.isPaused && session.pausedAtUtc) {
    local.pausedAt = session.pausedAtUtc;
    local.pausedBy = "user";
  }
  if ((session.totalPausedSeconds ?? 0) > 0) {
    local.totalPausedSeconds = session.totalPausedSeconds;
  }

  return local;
};

const reconcileActiveSessionFromCloud = async (): Promise<void> => {
  const auth = await loadFocusbotAuthSession();
  if (!auth?.accessToken) return;

  const remote = await getActiveCloudSession();
  if (!remote) {
    return;
  }

  const local = await loadActiveSession();
  const localServerSessionId = await getStoredServerSessionId();
  const shouldReplaceLocal =
    !local || local.sessionId !== remote.id || localServerSessionId !== remote.id;

  await chrome.storage.local.set({ [APP_KEYS.serverSessionId]: remote.id });

  if (shouldReplaceLocal) {
    await saveActiveSession(buildLocalSessionFromServer(remote));
    await startBadgeInterval();
  }

  await broadcastStateUpdate();
};

const endLocalSessionFromRemoteEvent = async (endedAtUtc: string): Promise<void> => {
  const session = await loadActiveSession();
  if (!session) return;

  if (session.currentVisit?.tabId !== undefined) {
    await sendDistractionAlert(session.currentVisit.tabId, { show: false });
  }

  const endedAt = endedAtUtc || nowIso();
  if (session.pausedAt) {
    session.totalPausedSeconds =
      (session.totalPausedSeconds ?? 0) + secondsBetween(session.pausedAt, endedAt);
  }
  finalizeCurrentVisit(session, endedAt);
  session.endedAt = endedAt;
  const totalPaused = session.totalPausedSeconds ?? 0;
  session.summary = calculateSessionSummary(
    session.taskText,
    session.startedAt,
    endedAt,
    session.visits,
    totalPaused
  );

  const completedSession: CompletedSession = {
    sessionId: session.sessionId,
    taskText: session.taskText,
    taskHints: session.taskHints,
    startedAt: session.startedAt,
    endedAt,
    summary: session.summary
  };

  await saveCompletedSession(completedSession);
  await pruneOldSessions(100, 90);
  await saveLastSummary(session.summary);
  await saveActiveSession(null);
  stopBadgeInterval();
};

const handleSignalRSessionStarted = async (event: SessionStartedEvent): Promise<void> => {
  await chrome.storage.local.set({ [APP_KEYS.serverSessionId]: event.sessionId });
  const local = await loadActiveSession();
  if (!local || local.sessionId !== event.sessionId) {
    await saveActiveSession(
      buildLocalSessionFromServer({
        id: event.sessionId,
        sessionTitle: event.sessionTitle,
        sessionContext: event.sessionContext,
        startedAtUtc: event.startedAtUtc
      })
    );
    await startBadgeInterval();
  }
  await broadcastStateUpdate();
};

const handleSignalRSessionEnded = async (event: SessionEndedEvent): Promise<void> => {
  const storedServerSessionId = await getStoredServerSessionId();
  if (storedServerSessionId === event.sessionId) {
    await endLocalSessionFromRemoteEvent(event.endedAtUtc);
  }
  await chrome.storage.local.remove(APP_KEYS.serverSessionId);
  await broadcastStateUpdate();
};

const handleSignalRSessionPaused = async (_event: SessionPausedEvent): Promise<void> => {
  const local = await loadActiveSession();
  if (local && local.sessionId === _event.sessionId) {
    local.pausedAt = _event.pausedAtUtc;
    local.pausedBy = "user";
    await saveActiveSession(local);
  }
  await broadcastStateUpdate();
};

const handleSignalRSessionResumed = async (_event: SessionResumedEvent): Promise<void> => {
  const local = await loadActiveSession();
  if (local && local.sessionId === _event.sessionId && local.pausedAt) {
    const now = nowIso();
    local.totalPausedSeconds =
      (local.totalPausedSeconds ?? 0) + secondsBetween(local.pausedAt, now);
    delete local.pausedAt;
    delete local.pausedBy;
    await saveActiveSession(local);
    await startBadgeInterval();
  }
  await broadcastStateUpdate();
};

const handleSignalRClassificationChanged = async (event: ClassificationChangedEvent): Promise<void> => {
  if (event.source === "extension") {
    return;
  }

  if (event.source === "desktop") {
    console.info("[Foqus] Windows app foreground classification (SignalR):", {
      score: event.score,
      reason: event.reason,
      activityName: event.activityName,
      classifiedAtUtc: event.classifiedAtUtc,
      cached: event.cached
    });
  }

  await runExclusive(async () => {
    const session = await loadActiveSession();
    if (!session || session.pausedAt) {
      return;
    }

    const classification = mapApiScoreToClassification(event.score);
    const confidence = Math.min(1, Math.max(0, event.score / 10));
    const reasonWithActivity =
      event.activityName.trim().length > 0
        ? `[${event.activityName}] ${event.reason}`
        : event.reason;

    session.lastHubClassification = {
      score: event.score,
      reason: reasonWithActivity,
      activityName: event.activityName,
      source: event.source,
      classifiedAtUtc: event.classifiedAtUtc
    };

    if (!session.currentVisit) {
      await saveActiveSession(session);
      await broadcastStateUpdate();
      await updateIconState();
      return;
    }

    if (session.currentVisit.tabId !== undefined && session.currentVisit.tabId >= 0) {
      await sendDistractionAlert(session.currentVisit.tabId, { show: false });
    }

    session.currentVisit = {
      ...session.currentVisit,
      visitState: "classified",
      classification,
      confidence,
      reason: reasonWithActivity,
      score: event.score,
      reusedClassification: true
    };

    await saveActiveSession(session);
    await broadcastStateUpdate();
    await updateIconState();
  });
};

const ensureFocusHubConnected = async (): Promise<void> => {
  const auth = await loadFocusbotAuthSession();
  if (!auth?.accessToken) {
    await disconnectFocusHub();
    return;
  }

  await connectFocusHub({
    onSessionStarted: (event) => {
      void runExclusive(async () => {
        await handleSignalRSessionStarted(event);
      });
    },
    onSessionEnded: (event) => {
      void runExclusive(async () => {
        await handleSignalRSessionEnded(event);
      });
    },
    onSessionPaused: (event) => {
      void runExclusive(async () => {
        await handleSignalRSessionPaused(event);
      });
    },
    onSessionResumed: (event) => {
      void runExclusive(async () => {
        await handleSignalRSessionResumed(event);
      });
    },
    onClassificationChanged: (event) => {
      void handleSignalRClassificationChanged(event);
    },
    onReconnected: () => {
      void runExclusive(async () => {
        await reconcileActiveSessionFromCloud();
      });
    }
  });
};

const getBrowserName = (): string => {
  const ua = navigator.userAgent;
  if (ua.includes("Edg/")) return "Edge";
  if (ua.includes("Chrome/")) return "Chrome";
  if (ua.includes("Firefox/")) return "Firefox";
  if (ua.includes("Safari/")) return "Safari";
  return "Browser";
};

/**
 * Registers the extension as a client and stores the returned client id.
 * Uses a stable fingerprint (UUID that persists in storage).
 */
const ensureClientRegistered = async (): Promise<void> => {
  const existingClientId = await getStoredClientId();
  if (existingClientId) return;

  const stored = await chrome.storage.local.get([
    APP_KEYS.clientFingerprint,
    APP_KEYS.deviceFingerprint
  ]);
  let fingerprint = (stored[APP_KEYS.clientFingerprint] ?? stored[APP_KEYS.deviceFingerprint]) as
    | string
    | undefined;
  if (!fingerprint) {
    fingerprint = crypto.randomUUID();
  }
  await chrome.storage.local.set({
    [APP_KEYS.clientFingerprint]: fingerprint,
    [APP_KEYS.deviceFingerprint]: fingerprint
  });

  const manifest = chrome.runtime.getManifest();
  const result = await registerClient({
    clientType: "Extension",
    host: getClientHostFromUserAgent(),
    name: getBrowserName(),
    fingerprint,
    appVersion: manifest.version,
    platform: navigator.userAgent
  });

  if (result?.id) {
    await chrome.storage.local.set({
      [APP_KEYS.clientId]: result.id,
      [APP_KEYS.deviceId]: result.id
    });
  }
};

// ---------------------------------------------------------------------------
// State queue
// ---------------------------------------------------------------------------

let stateMutationQueue: Promise<void> = Promise.resolve();

const runExclusive = async <T>(handler: () => Promise<T>): Promise<T> => {
  const completion = stateMutationQueue.then(async () => undefined);
  let release: () => void = () => undefined;
  stateMutationQueue = new Promise<void>((resolve) => {
    release = resolve;
  });

  await completion;

  try {
    return await handler();
  } finally {
    release();
  }
};

const toRuntimeState = async () => ({
  settings: await loadSettings(),
  activeSession: await loadActiveSession(),
  lastSummary: await loadLastSummary(),
  lastError: await loadLastError(),
  isAuthenticated: Boolean((await loadFocusbotAuthSession())?.accessToken)
});

const broadcastStateUpdate = async (): Promise<void> => {
  try {
    const state = await toRuntimeState();
    await chrome.runtime.sendMessage({ type: "STATE_UPDATED", data: state });
  } catch {
    // Expected when no UI (popup/sidepanel) is open. UI will fetch state when it opens.
  }
  
  try {
    await updateIconState();
  } catch (error) {
    console.error("Icon state update error:", error);
  }
};

const BADGE_ALARM_NAME = "focusbot-badge-tick";
const BADGE_UPDATE_INTERVAL_MS = 5000;

let badgeIntervalId: ReturnType<typeof setInterval> | null = null;

const startBadgeInterval = async (): Promise<void> => {
  if (badgeIntervalId) {
    clearInterval(badgeIntervalId);
    badgeIntervalId = null;
  }
  const session = await loadActiveSession();
  if (!session) return;
  await chrome.alarms.create(BADGE_ALARM_NAME, { periodInMinutes: 1 });
  badgeIntervalId = setInterval(() => void updateIconState(), BADGE_UPDATE_INTERVAL_MS);
};

const stopBadgeInterval = (): void => {
  if (badgeIntervalId) {
    clearInterval(badgeIntervalId);
    badgeIntervalId = null;
  }
  void chrome.alarms.clear(BADGE_ALARM_NAME);
};

const getIconStateFromSession = (session: FocusSession | null): IconState => {
  if (!session) {
    return "default";
  }

  if (session.pausedAt) {
    return "default";
  }

  if (!session.currentVisit) {
    const hub = session.lastHubClassification;
    if (hub) {
      const c = mapApiScoreToClassification(hub.score);
      if (c === "aligned") {
        return "aligned";
      }
      if (c === "neutral") {
        return "neutral";
      }
      return "distracting";
    }
    return "default";
  }

  if (session.currentVisit.visitState === "classifying") {
    return "analyzing";
  }

  if (session.currentVisit.visitState === "error") {
    return "error";
  }

  if (session.currentVisit.classification === "aligned") {
    return "aligned";
  }

  if (session.currentVisit.classification === "neutral") {
    return "neutral";
  }

  if (session.currentVisit.classification === "distracting") {
    return "distracting";
  }

  return "default";
};

const updateIconState = async (): Promise<void> => {
  try {
    const session = await loadActiveSession();
    const iconState = getIconStateFromSession(session);

    const badgeConfig: Record<IconState, { color: string }> = {
      default: { color: "#6366f1" },
      aligned: { color: "#10b981" },
      neutral: { color: "#f59e0b" },
      distracting: { color: "#ef4444" },
      analyzing: { color: "#a855f7" },
      error: { color: "#a855f7" }
    };

    const config = badgeConfig[iconState];
    const badgeText =
      session !== null
        ? String(Math.round(calculateLiveSummary(session).focusPercentage))
        : "";

    await chrome.action.setBadgeText({ text: badgeText });
    await chrome.action.setBadgeBackgroundColor({ color: config.color });

    const stateLabels: Record<IconState, string> = {
      default: "Foqus",
      aligned: "Foqus - Aligned",
      neutral: "Foqus - Neutral",
      distracting: "Foqus - Distracting",
      analyzing: "Foqus - Analyzing",
      error: "Foqus - Error"
    };

    const title =
      session?.pausedAt != null ? "Foqus - Paused" : stateLabels[iconState];
    await chrome.action.setTitle({ title });
  } catch (error) {
    console.error("Failed to update icon:", error);
  }
};

const createImageData = async (dataUrl: string, size: number): Promise<ImageData> => {
  return new Promise((resolve, reject) => {
    const img = new (globalThis.Image || (require("canvas") as any).Image)();
    img.onload = () => {
      const canvas = new OffscreenCanvas(size, size);
      const ctx = canvas.getContext("2d");
      if (!ctx) {
        reject(new Error("Failed to get canvas context"));
        return;
      }
      ctx.drawImage(img, 0, 0, size, size);
      const imageData = ctx.getImageData(0, 0, size, size);
      resolve(imageData);
    };
    img.onerror = () => reject(new Error("Failed to load image"));
    img.src = dataUrl;
  });
};

const sendDistractionAlert = async (
  tabId: number,
  payload: { show: boolean; sessionId?: string; taskText?: string; domain?: string; reason?: string }
): Promise<void> => {
  try {
    await chrome.tabs.sendMessage(tabId, {
      type: "FOCUSBOT_DISTRACTION_ALERT",
      ...payload
    });
  } catch {
    // Ignore message failures for tabs where content script is unavailable.
  }
};

const handleContentReady = async (tabId: number): Promise<void> => {
  const tab = await chrome.tabs.get(tabId).catch(() => null);
  if (!tab?.url || !isTrackableUrl(tab.url)) {
    return;
  }
  await updateVisitFromTab(tab);
  const session = await loadActiveSession();
  if (
    !session?.currentVisit ||
    session.pausedAt ||
    session.currentVisit.tabId !== tabId ||
    session.currentVisit.visitState !== "classified" ||
    session.currentVisit.classification !== "distracting" ||
    session.currentVisit.reusedClassification
  ) {
    return;
  }
  await sendDistractionAlert(tabId, {
    show: true,
    sessionId: session.sessionId,
    taskText: session.taskText,
    domain: session.currentVisit.domain,
    reason: session.currentVisit.reason
  });
};

const finalizeCurrentVisit = (session: FocusSession, leftAt: string): void => {
  if (!session.currentVisit) {
    return;
  }

  if (session.currentVisit.visitState !== "classified" || !session.currentVisit.classification) {
    session.currentVisit = undefined;
    return;
  }

  const enteredAt = Date.parse(session.currentVisit.enteredAt);
  const exitedAt = Date.parse(leftAt);
  const durationSeconds = Math.max(0, Math.round((exitedAt - enteredAt) / 1000));
  if (durationSeconds <= 0) {
    session.currentVisit = undefined;
    return;
  }

  session.visits.push({
    pageVisitId: createId(),
    sessionId: session.sessionId,
    tabId: session.currentVisit.tabId,
    url: session.currentVisit.url,
    domain: session.currentVisit.domain,
    title: session.currentVisit.title,
    enteredAt: session.currentVisit.enteredAt,
    leftAt,
    durationSeconds,
    classification: session.currentVisit.classification,
    confidence: session.currentVisit.confidence ?? 0,
    reason: session.currentVisit.reason
  });
  session.currentVisit = undefined;
};

const classifyWithPolicy = async (
  session: FocusSession,
  taskUrl: string,
  title: string
): Promise<{ ok: true; result: ClassificationResult } | { ok: false; error: string }> => {
  const settings = await loadSettings();

  let lastError = "Unknown classifier failure.";
  for (let attempt = 1; attempt <= 3; attempt += 1) {
    try {
      return {
        ok: true,
        result: await classifyPage(settings, session.taskText, taskUrl, title, 8000, session.taskHints)
      };
    } catch (error) {
      lastError = error instanceof Error ? error.message : "Classification failed.";
      if (attempt < 3) {
        await sleep(300 * attempt);
      }
    }
  }

  return {
    ok: false,
    error: `Could not classify this page after retries. ${lastError}`
  };
};

const classifyAndApplyVisit = async (
  sessionId: string,
  visitToken: string,
  tabId: number,
  url: string,
  title: string
): Promise<void> => {
  const sessionAtCallStart = await loadActiveSession();
  if (!sessionAtCallStart || sessionAtCallStart.sessionId !== sessionId) {
    return;
  }

  const outcome = await classifyWithPolicy(sessionAtCallStart, url, title);

  await runExclusive(async () => {
    const latest = await loadActiveSession();
    if (!latest || latest.sessionId !== sessionId || !latest.currentVisit || latest.currentVisit.visitToken !== visitToken) {
      return;
    }

    if (outcome.ok) {
      console.info("[Foqus] Classification complete:", {
        classification: outcome.result.classification,
        score: outcome.result.score,
        url: latest.currentVisit.url,
        title: latest.currentVisit.title,
        domain: latest.currentVisit.domain,
        reason: outcome.result.reason
      });

      latest.currentVisit = {
        ...latest.currentVisit,
        visitState: "classified",
        classification: outcome.result.classification,
        confidence: outcome.result.confidence,
        reason: outcome.result.reason,
        score: outcome.result.score
      };
      await saveLastError(null);
      await saveActiveSession(latest);
      await broadcastStateUpdate();

      const showDistraction =
        !latest.pausedAt && outcome.result.classification === "distracting";
      await sendDistractionAlert(tabId, {
        show: showDistraction,
        sessionId: latest.sessionId,
        taskText: latest.taskText,
        domain: latest.currentVisit.domain,
        reason: outcome.result.reason
      });
      return;
    }

    latest.currentVisit = {
      ...latest.currentVisit,
      visitState: "error",
      classification: undefined,
      confidence: undefined,
      reason: outcome.error
    };
    await saveLastError(outcome.error);
    await saveActiveSession(latest);
    await broadcastStateUpdate();
    await sendDistractionAlert(tabId, { show: false });
  });
};

const updateVisitFromTab = async (tab: chrome.tabs.Tab): Promise<void> =>
  runExclusive(async () => {
    const session = await loadActiveSession();
    if (!session || tab.id === undefined || !tab.url || !isTrackableUrl(tab.url)) {
      return;
    }
    if (session.pausedAt) {
      return;
    }

    // Don't replace a desktop visit when the browser is not the focused window.
    // Tab events (onUpdated, onActivated) can fire for background tabs.
    if (session.currentVisit?.tabId === -1 && !browserWindowInForeground) {
      return;
    }

    const title = tab.title ?? getDomain(tab.url);
    const domain = getDomain(tab.url);
    const current = session.currentVisit;

    // If the page hasn't changed and we already have a classified result (not from hub mirroring),
    // skip re-classification. This prevents spam when the 5s badge interval triggers updateIconState.
    const isSamePageWithValidClassification =
      current &&
      current.tabId === tab.id &&
      current.url === tab.url &&
      current.title === title &&
      current.domain === domain &&
      current.visitState === "classified" &&
      !current.reusedClassification;

    if (isSamePageWithValidClassification) {
      return;
    }

    const transitionAt = nowIso();
    const sameTabSameDomainDistracting =
      current &&
      current.tabId === tab.id &&
      current.domain === domain &&
      current.visitState === "classified" &&
      current.classification === "distracting" &&
      !current.reusedClassification;

    if (current?.tabId !== undefined) {
      await sendDistractionAlert(current.tabId, { show: false });
    }
    finalizeCurrentVisit(session, transitionAt);

    const visitToken = createId();
    if (sameTabSameDomainDistracting) {
      const nextVisit: InProgressVisit = {
        visitToken,
        tabId: tab.id,
        url: tab.url,
        domain,
        title,
        enteredAt: transitionAt,
        visitState: "classified",
        classification: "distracting",
        confidence: current.confidence ?? 0.5,
        reason: current.reason,
        reusedClassification: true
      };
      session.currentVisit = nextVisit;
      await saveActiveSession(session);
      await broadcastStateUpdate();
      return;
    }

    const nextVisit: InProgressVisit = {
      visitToken,
      tabId: tab.id,
      url: tab.url,
      domain,
      title,
      enteredAt: transitionAt,
      visitState: "classifying",
      reason: "Analyzing URL and page title for task alignment."
    };
    session.currentVisit = nextVisit;
    await saveActiveSession(session);
    await broadcastStateUpdate();

    void classifyAndApplyVisit(session.sessionId, visitToken, tab.id, tab.url, title);
  });

const captureCurrentActiveTab = async (): Promise<void> => {
  const [activeTab] = await chrome.tabs.query({ active: true, lastFocusedWindow: true });
  if (activeTab) {
    await updateVisitFromTab(activeTab);
  }
};

const startSession = async (taskText: string, taskHints?: string): Promise<RuntimeResponse<FocusSession>> =>
  runExclusive(async () => {
    const trimmedTask = taskText.trim();
    if (!trimmedTask) {
      return { ok: false, error: "Task is required to start a session." };
    }

    const currentSession = await loadActiveSession();
    if (currentSession) {
      return { ok: false, error: "Only one active session is allowed." };
    }

    const settings = await loadSettings();
    const auth = await loadFocusbotAuthSession();
    if (!auth?.accessToken) {
      return { ok: false, error: "Sign in to start a focus session. Click Settings to sign in." };
    }
    if (planRequiresApiKey(settings.plan) && !settings.openAiApiKey.trim()) {
      return { ok: false, error: "OpenAI API key is required for your plan. Add it in Settings first." };
    }

    const session: FocusSession = {
      sessionId: createId(),
      taskText: trimmedTask,
      taskHints: taskHints?.trim() || undefined,
      startedAt: nowIso(),
      visits: []
    };
    await saveLastError(null);
    await saveActiveSession(session);
    await startBadgeInterval();
    void captureCurrentActiveTab();

    // Submit cloud session start for all signed-in users.
    const clientId = await getStoredClientId();
    void startCloudSession(session.taskText, clientId, session.taskHints)
      .then(async (cloudSession) => {
        if (cloudSession?.id) {
          await chrome.storage.local.set({ [APP_KEYS.serverSessionId]: cloudSession.id });
        }
      })
      .catch(async () => {
        await enqueueOfflineItem({
          id: createId(),
          type: "session-start",
          payload: { taskText: session.taskText, taskHints: session.taskHints, clientId, localSessionId: session.sessionId },
          createdAt: nowIso(),
          retryCount: 0
        });
      });

    const latest = await loadActiveSession();
    await broadcastStateUpdate();

    return { ok: true, data: latest ?? session };
  });

const IDLE_DETECTION_INTERVAL_SECONDS = 300;

const pauseSession = async (
  reason: "user" | "idle" = "user",
  effectivePausedAt?: string
): Promise<RuntimeResponse<FocusSession>> =>
  runExclusive(async () => {
    const session = await loadActiveSession();
    if (!session) {
      return { ok: false, error: "No active session to pause." };
    }
    if (session.pausedAt) {
      return { ok: false, error: "Session is already paused." };
    }

    const serverSessionId = await resolveServerSessionIdForSync(session.sessionId);
    const targetSession = (await loadActiveSession()) ?? session;
    if (serverSessionId) {
      const remote = await pauseCloudSession(serverSessionId);
      if (!remote) {
        return { ok: false, error: "Could not pause session on server." };
      }
      targetSession.pausedAt = remote.pausedAtUtc ?? nowIso();
      targetSession.pausedBy = reason;
      targetSession.totalPausedSeconds = remote.totalPausedSeconds;
      const tabIdToHide = targetSession.currentVisit?.tabId;
      finalizeCurrentVisit(targetSession, targetSession.pausedAt);
      if (tabIdToHide !== undefined) {
        await sendDistractionAlert(tabIdToHide, { show: false });
      }
      await saveActiveSession(targetSession);
      await broadcastStateUpdate();
      return { ok: true, data: targetSession };
    }

    let pausedAt: string;
    if (reason === "idle" && effectivePausedAt) {
      const enteredAt = session.currentVisit?.enteredAt;
      pausedAt =
        enteredAt && effectivePausedAt < enteredAt ? enteredAt : effectivePausedAt;
    } else {
      pausedAt = nowIso();
    }

    session.pausedAt = pausedAt;
    session.pausedBy = reason;
    const tabIdToHide = session.currentVisit?.tabId;
    finalizeCurrentVisit(session, pausedAt);
    if (tabIdToHide !== undefined) {
      await sendDistractionAlert(tabIdToHide, { show: false });
    }
    await saveActiveSession(session);
    await broadcastStateUpdate();

    return { ok: true, data: session };
  });

const resumeSession = async (): Promise<RuntimeResponse<FocusSession>> =>
  runExclusive(async () => {
    const session = await loadActiveSession();
    if (!session) {
      return { ok: false, error: "No active session to resume." };
    }
    if (!session.pausedAt) {
      return { ok: false, error: "Session is not paused." };
    }

    const serverSessionId = await resolveServerSessionIdForSync(session.sessionId);
    const targetSession = (await loadActiveSession()) ?? session;
    if (serverSessionId) {
      const remote = await resumeCloudSession(serverSessionId);
      if (!remote) {
        return { ok: false, error: "Could not resume session on server." };
      }
      targetSession.totalPausedSeconds = remote.totalPausedSeconds;
      delete targetSession.pausedAt;
      delete targetSession.pausedBy;
      await saveActiveSession(targetSession);
      await broadcastStateUpdate();
      await startBadgeInterval();
      void captureCurrentActiveTab();
      const latest = await loadActiveSession();
      return { ok: true, data: latest ?? targetSession };
    }

    const now = nowIso();
    const currentPauseSeconds = secondsBetween(session.pausedAt, now);
    session.totalPausedSeconds = (session.totalPausedSeconds ?? 0) + currentPauseSeconds;
    delete session.pausedAt;
    delete session.pausedBy;
    await saveActiveSession(session);
    await broadcastStateUpdate();
    await startBadgeInterval();

    void captureCurrentActiveTab();

    const latest = await loadActiveSession();
    return { ok: true, data: latest ?? session };
  });

const endSession = async (): Promise<RuntimeResponse<CompletedSession>> =>
  runExclusive(async () => {
    const session = await loadActiveSession();
    if (!session) {
      return { ok: false, error: "No active session to end." };
    }

    const endedAt = nowIso();
    if (session.currentVisit?.tabId !== undefined) {
      await sendDistractionAlert(session.currentVisit.tabId, { show: false });
    }
    if (session.pausedAt) {
      session.totalPausedSeconds =
        (session.totalPausedSeconds ?? 0) + secondsBetween(session.pausedAt, endedAt);
    }
    finalizeCurrentVisit(session, endedAt);
    session.endedAt = endedAt;
    const totalPaused = session.totalPausedSeconds ?? 0;
    session.summary = calculateSessionSummary(
      session.taskText,
      session.startedAt,
      endedAt,
      session.visits,
      totalPaused
    );

    const completedSession: CompletedSession = {
      sessionId: session.sessionId,
      taskText: session.taskText,
      taskHints: session.taskHints,
      startedAt: session.startedAt,
      endedAt,
      summary: session.summary
    };
    await saveCompletedSession(completedSession);
    await pruneOldSessions(100, 90);
    await saveLastSummary(session.summary);
    await saveActiveSession(null);
    stopBadgeInterval();

    // Submit cloud session end for all signed-in users.
    const serverSessionId = await getStoredServerSessionId();
    const clientId = await getStoredClientId();
    if (serverSessionId) {
      const summaryPayload = session.summary as unknown as Record<string, unknown>;
      void endCloudSession(serverSessionId, summaryPayload, clientId)
        .catch(async () => {
          await enqueueOfflineItem({
            id: createId(),
            type: "session-end",
            payload: { serverSessionId, summary: summaryPayload, clientId },
            createdAt: nowIso(),
            retryCount: 0
          });
        });
      await chrome.storage.local.remove(APP_KEYS.serverSessionId);
    }

    await broadcastStateUpdate();

    return { ok: true, data: completedSession };
  });

const handleRequest = async (request: RuntimeRequest): Promise<RuntimeResponse> => {
  switch (request.type) {
    case "GET_STATE":
      return { ok: true, data: await toRuntimeState() };
    case "START_SESSION":
      return startSession(request.taskText, request.taskHints);
    case "END_SESSION":
      return endSession();
    case "PAUSE_SESSION":
      return pauseSession();
    case "RESUME_SESSION":
      return resumeSession();
    case "GET_ANALYTICS": {
      const sessions = await loadCompletedSessions();
      return { ok: true, data: calculateAnalytics(request.range, sessions) };
    }
    case "UPDATE_SETTINGS": {
      const current = await loadSettings();
      const updated = await patchSettings({
        ...request.payload,
        onboardingCompleted: request.payload.onboardingCompleted ?? true,
        classifierModel: request.payload.classifierModel ?? current.classifierModel
      });
      await broadcastStateUpdate();
      return { ok: true, data: updated };
    }
    case "CLEAR_ERROR":
      await saveLastError(null);
      await broadcastStateUpdate();
      return { ok: true };
    case "OPEN_OPTIONS":
      await chrome.runtime.openOptionsPage();
      return { ok: true };
    case "OPEN_SIDE_PANEL": {
      const currentWindow = await chrome.windows.getCurrent();
      if (!currentWindow.id) {
        await saveLastError("Unable to open side panel in current window.");
        await broadcastStateUpdate();
        return { ok: false, error: "Unable to open side panel in current window." };
      }
      await chrome.sidePanel.open({ windowId: currentWindow.id });
      return { ok: true };
    }
    case "GET_INTEGRATION_STATE":
      return { ok: true, data: getStaticIntegrationState() };
    case "SIGN_OUT": {
      const clientIdToDeregister = await getStoredClientId();
      if (clientIdToDeregister) {
        void deregisterClient(clientIdToDeregister).catch(() => {});
      }
      await disconnectFocusHub();
      await clearFocusbotAuthSession();
      await patchSettings({
        focusbotEmail: undefined
      });
      await chrome.alarms.clear(SIGNALR_RECONNECT_ALARM_NAME);
      await broadcastStateUpdate();
      return { ok: true };
    }
    case "REFRESH_PLAN": {
      const sub = await getSubscriptionStatus();
      if (sub) {
        const plan = mapBackendPlanType(sub.planType);
        if (!plan) {
          return { ok: false, error: `Unknown plan type: ${sub.planType}` };
        }
        await patchSettings({
          plan,
          subscriptionStatus: sub.status,
          serverPlanType: sub.planType,
          trialEndsAt: sub.trialEndsAt,
          currentPeriodEndsAt: sub.currentPeriodEndsAt
        });
        await broadcastStateUpdate();
      }
      return { ok: true };
    }
    default:
      return { ok: false, error: "Unknown request." };
  }
};

const FOCUSBOT_AUTH_SESSION_STORED = "FOCUSBOT_AUTH_SESSION_STORED";
const FOCUSBOT_AUTH_FROM_EXTENSION_CALLBACK = "FOCUSBOT_AUTH_FROM_EXTENSION_CALLBACK";

const finalizeFocusbotSignInAfterTokensStored = async (email: string | undefined): Promise<void> => {
  await patchSettings({
    focusbotEmail: email,
    onboardingCompleted: true
  });

  await fetchCurrentUser();

  const sub = await getSubscriptionStatus();
  if (sub) {
    const plan = mapBackendPlanType(sub.planType);
    if (!plan) {
      throw new Error(`Unknown plan type: ${sub.planType}`);
    }
    await patchSettings({
      plan,
      subscriptionStatus: sub.status,
      serverPlanType: sub.planType,
      trialEndsAt: sub.trialEndsAt,
      currentPeriodEndsAt: sub.currentPeriodEndsAt
    });
  }

  await ensureClientRegistered();
  await ensureFocusHubConnected();
  await reconcileActiveSessionFromCloud();
  await chrome.alarms.create(SIGNALR_RECONNECT_ALARM_NAME, { periodInMinutes: 1 });

  await broadcastStateUpdate();
};

chrome.runtime.onMessage.addListener((message: unknown, sender: chrome.runtime.MessageSender, sendResponse) => {
  if (
    message &&
    typeof message === "object" &&
    "type" in message &&
    (message as { type: string }).type === "FOCUSBOT_CONTENT_READY" &&
    sender.tab?.id
  ) {
    handleContentReady(sender.tab.id)
      .then(() => sendResponse({ ok: true }))
      .catch(() => sendResponse({ ok: false }));
    return true;
  }
  if (
    message &&
    typeof message === "object" &&
    "type" in message &&
    (message as { type: string }).type === FOCUSBOT_AUTH_FROM_EXTENSION_CALLBACK
  ) {
    const m = message as {
      type: string;
      accessToken?: unknown;
      refreshToken?: unknown;
      email?: unknown;
    };
    const accessToken = typeof m.accessToken === "string" ? m.accessToken : "";
    const refreshToken = typeof m.refreshToken === "string" ? m.refreshToken : "";
    const email = typeof m.email === "string" ? m.email : "";
    if (!accessToken || !email) {
      sendResponse({ ok: false });
      return true;
    }

    runExclusive(async () => {
      await chrome.storage.local.set({
        "focusbot.supabaseAccessToken": accessToken,
        "focusbot.supabaseRefreshToken": refreshToken,
        "focusbot.supabaseEmail": email
      });
      await finalizeFocusbotSignInAfterTokensStored(email);
    })
      .then(async () => {
        await openSidePanelForAuthWindow(sender);
        sendResponse({ ok: true });
      })
      .catch(() => sendResponse({ ok: false }));
    return true;
  }
  if (
    message &&
    typeof message === "object" &&
    "type" in message &&
    (message as { type: string }).type === FOCUSBOT_AUTH_SESSION_STORED
  ) {
    const email = "email" in message && typeof (message as { email?: unknown }).email === "string"
      ? (message as { email: string }).email
      : undefined;

    runExclusive(async () => {
      await finalizeFocusbotSignInAfterTokensStored(email);
    })
      .then(async () => {
        await openSidePanelForAuthWindow(sender);
        sendResponse({ ok: true });
      })
      .catch(() => sendResponse({ ok: false }));
    return true;
  }
  handleRequest(message as RuntimeRequest)
    .then((response) => sendResponse(response))
    .catch(async (error: unknown) => {
      const errorMessage = error instanceof Error ? error.message : "Unhandled runtime error.";
      await saveLastError(errorMessage);
      await broadcastStateUpdate();
      sendResponse({
        ok: false,
        error: errorMessage
      } satisfies RuntimeResponse);
    });
  return true;
});

chrome.tabs.onActivated.addListener(async ({ tabId }) => {
  const tab = await chrome.tabs.get(tabId);
  await updateVisitFromTab(tab);
});

chrome.tabs.onUpdated.addListener(async (_tabId, changeInfo, tab) => {
  if (!tab.active || (!changeInfo.url && changeInfo.status !== "complete")) {
    return;
  }
  await updateVisitFromTab(tab);
});

chrome.windows.onFocusChanged.addListener(async (windowId) => {
  if (windowId === chrome.windows.WINDOW_ID_NONE) {
    browserWindowInForeground = false;
    return;
  }

  browserWindowInForeground = true;

  const [tab] = await chrome.tabs.query({ active: true, windowId });
  if (tab) {
    await updateVisitFromTab(tab);
  }
});

chrome.alarms.onAlarm.addListener(async (alarm) => {
  if (alarm.name === BADGE_ALARM_NAME) {
    await updateIconState();
    await startBadgeInterval();
  }
  if (alarm.name === SIGNALR_RECONNECT_ALARM_NAME) {
    await ensureFocusHubConnected();
  }
});

chrome.runtime.onStartup.addListener(async () => {
  await migrateSessionsToCompletedSessions();
  await startBadgeInterval();
  await captureCurrentActiveTab();
  startExtensionPresence();
  const session = await loadFocusbotAuthSession();
  if (session) {
    void ensureClientRegistered();
    void ensureFocusHubConnected();
    void reconcileActiveSessionFromCloud();
    void chrome.alarms.create(SIGNALR_RECONNECT_ALARM_NAME, { periodInMinutes: 1 });
  }
});

const migrateSessionsToCompletedSessions = async (): Promise<void> => {
  const stored = await chrome.storage.local.get(APP_KEYS.completedSessions);
  if (APP_KEYS.completedSessions in stored) {
    return;
  }
  const oldSessions = await loadSessions();
  const completed: CompletedSession[] = [];
  for (const session of oldSessions) {
    if (!session.endedAt || !session.summary) {
      continue;
    }
    completed.push({
      sessionId: session.sessionId,
      taskText: session.taskText,
      taskHints: session.taskHints,
      startedAt: session.startedAt,
      endedAt: session.endedAt,
      summary: session.summary
    });
  }
  if (completed.length > 0) {
    completed.sort((a, b) => Date.parse(b.endedAt) - Date.parse(a.endedAt));
    await setCompletedSessions(completed);
  }
};

chrome.runtime.onInstalled.addListener(async () => {
  await migrateSessionsToCompletedSessions();
  await startBadgeInterval();
  await captureCurrentActiveTab();
  startExtensionPresence();
  const session = await loadFocusbotAuthSession();
  if (session) {
    void ensureClientRegistered();
    void ensureFocusHubConnected();
    void reconcileActiveSessionFromCloud();
    void chrome.alarms.create(SIGNALR_RECONNECT_ALARM_NAME, { periodInMinutes: 1 });
  }
});

chrome.idle.setDetectionInterval(IDLE_DETECTION_INTERVAL_SECONDS);

chrome.idle.onStateChanged.addListener((newState: string) => {
  if (newState === "idle" || newState === "locked") {
    void runExclusive(async () => {
      const session = await loadActiveSession();
      if (!session || session.pausedAt) return;
      const effectivePausedAt = new Date(
        Date.now() - IDLE_DETECTION_INTERVAL_SECONDS * 1000
      ).toISOString();
      await pauseSession("idle", effectivePausedAt);
    });
    return;
  }
  if (newState === "active") {
    void runExclusive(async () => {
      const session = await loadActiveSession();
      if (!session?.pausedAt || session.pausedBy !== "idle") return;
      await resumeSession();
    });
  }
});

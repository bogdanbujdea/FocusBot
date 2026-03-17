import { calculateAnalytics } from "../shared/analytics";
import { classifyPage, classifyDesktopApp } from "../shared/classifier";
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
  setCompletedSessions
} from "../shared/storage";
import type {
  ClassificationResult,
  CompletedSession,
  FocusSession,
  InProgressVisit,
  RuntimeRequest,
  RuntimeResponse
} from "../shared/types";
import { APP_KEYS, createId, nowIso, secondsBetween, sleep } from "../shared/utils";
import { getDomain, isTrackableUrl, matchesExcludedDomain } from "../shared/url";
import { ICON_DATA_URLS, type IconState } from "../shared/types";
import {
  startIntegration,
  setMessageHandler,
  setHandshakeProvider,
  sendHandshake,
  sendTaskStarted,
  sendTaskEnded,
  sendFocusStatus,
  sendBrowserContext,
  getIntegrationState,
  updateLeaderTask,
  clearLeaderTask,
  updateLastFocusStatus,
  updateDesktopContext,
  updateBrowserForeground,
  onIntegrationStateChange,
  isConnected
} from "../shared/integration";
import type {
  IntegrationEnvelope,
  TaskStartedPayload,
  FocusStatusPayload,
  DesktopForegroundPayload
} from "../shared/integrationTypes";
import { MESSAGE_TYPES } from "../shared/integrationTypes";

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
  lastError: await loadLastError()
});

const broadcastStateUpdate = async (): Promise<void> => {
  try {
    const state = await toRuntimeState();
    console.log("Broadcasting state update:", state);
    await chrome.runtime.sendMessage({ type: "STATE_UPDATED", data: state });
  } catch (error) {
    console.log("Broadcast error (expected if no UI open):", error);
  }
  
  try {
    await updateIconState();
  } catch (error) {
    console.error("Icon state update error:", error);
  }
};

const handleIntegrationMessage = async (envelope: IntegrationEnvelope): Promise<void> => {
  const integrationState = getIntegrationState();

  switch (envelope.type) {
    case MESSAGE_TYPES.HANDSHAKE: {
      const payload = envelope.payload as { source: string; hasActiveTask: boolean; taskId?: string; taskText?: string; taskHints?: string } | undefined;
      if (!payload) break;

      const session = await loadActiveSession();
      sendHandshake(
        session !== null,
        session?.sessionId,
        session?.taskText,
        undefined,
        session?.startedAt
      );

      if (payload.hasActiveTask && payload.taskText) {
        if (session) {
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
          const sessionForHistory = stripActiveSessionForHistory(session);
          await saveSession(sessionForHistory);
          await saveActiveSession(null);
          stopBadgeInterval();
        }
        updateLeaderTask(payload.taskId ?? "", payload.taskText);
      }
      await pushBrowserContextToApp();
      await broadcastStateUpdate();
      break;
    }

    case MESSAGE_TYPES.TASK_STARTED: {
      const payload = envelope.payload as TaskStartedPayload | undefined;
      if (!payload) break;

      const sessionForStarted = await loadActiveSession();
      if (sessionForStarted) {
        const endedAt = nowIso();
        if (sessionForStarted.currentVisit?.tabId !== undefined) {
          await sendDistractionAlert(sessionForStarted.currentVisit.tabId, { show: false });
        }
        if (sessionForStarted.pausedAt) {
          sessionForStarted.totalPausedSeconds =
            (sessionForStarted.totalPausedSeconds ?? 0) + secondsBetween(sessionForStarted.pausedAt, endedAt);
        }
        finalizeCurrentVisit(sessionForStarted, endedAt);
        sessionForStarted.endedAt = endedAt;
        const totalPaused = sessionForStarted.totalPausedSeconds ?? 0;
        sessionForStarted.summary = calculateSessionSummary(
          sessionForStarted.taskText,
          sessionForStarted.startedAt,
          endedAt,
          sessionForStarted.visits,
          totalPaused
        );
        const sessionForHistory = stripActiveSessionForHistory(sessionForStarted);
        await saveSession(sessionForHistory);
        await saveActiveSession(null);
        stopBadgeInterval();
      }
      updateLeaderTask(payload.taskId, payload.taskText);
      await broadcastStateUpdate();
      break;
    }

    case MESSAGE_TYPES.TASK_ENDED: {
      clearLeaderTask();
      await broadcastStateUpdate();
      break;
    }

    case MESSAGE_TYPES.FOCUS_STATUS: {
      const payload = envelope.payload as FocusStatusPayload | undefined;
      if (!payload) break;

      if (getIntegrationState().leaderTaskId) {
        updateLastFocusStatus(payload);
      }
      await broadcastStateUpdate();
      break;
    }

    case MESSAGE_TYPES.DESKTOP_FOREGROUND: {
      const payload = envelope.payload as DesktopForegroundPayload | undefined;
      if (!payload) break;

      const session = await loadActiveSession();
      if (!session || session.pausedAt) break;

      const settings = await loadSettings();
      try {
        const result = await classifyDesktopApp(settings, session.taskText, payload.processName, payload.windowTitle, session.taskHints);

        updateDesktopContext({
          processName: payload.processName,
          windowTitle: payload.windowTitle,
          classification: result.classification,
          reason: result.reason ?? "",
          timestamp: Date.now()
        });

        sendFocusStatus({
          taskId: session.sessionId,
          classification: result.classification,
          reason: result.reason ?? "",
          score: result.classification === "aligned" ? 8 : 2,
          focusScorePercent: calculateLiveSummary(session).focusPercentage,
          contextType: "desktop",
          contextTitle: `${payload.processName} - ${payload.windowTitle}`
        });

        await broadcastStateUpdate();
      } catch (err) {
        console.error("[Integration] Desktop classification failed:", err);
      }
      updateBrowserForeground(false);
      break;
    }

  }
};

setMessageHandler(handleIntegrationMessage);
setHandshakeProvider(async () => {
  const session = await loadActiveSession();
  return {
    hasActiveTask: session !== null,
    taskId: session?.sessionId,
    taskText: session?.taskText,
    startedAt: session?.startedAt
  };
});

// Start desktop integration as soon as the background loads so we connect when the
// Windows app is running without requiring the user to open the popup first.
startIntegration();

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
  console.log("getIconStateFromSession called with:", { session: session ? { sessionId: session.sessionId, currentVisit: session.currentVisit } : null });
  
  if (!session) {
    console.log("No session, returning default");
    return "default";
  }

  if (session.pausedAt) {
    return "default";
  }

  if (!session.currentVisit) {
    console.log("No currentVisit, returning default");
    return "default";
  }

  if (session.currentVisit.visitState === "classifying") {
    console.log("Visit state is classifying, returning analyzing");
    return "analyzing";
  }

  if (session.currentVisit.visitState === "error") {
    console.log("Visit state is error, returning error");
    return "error";
  }

  if (session.currentVisit.classification === "aligned") {
    console.log("Classification is aligned, returning aligned");
    return "aligned";
  }

  if (session.currentVisit.classification === "distracting") {
    console.log("Classification is distracting, returning distracting");
    return "distracting";
  }

  console.log("No match, returning default");
  return "default";
};

const updateIconState = async (): Promise<void> => {
  try {
    const session = await loadActiveSession();
    const iconState = getIconStateFromSession(session);

    const badgeConfig: Record<IconState, { color: string }> = {
      default: { color: "#6366f1" },
      aligned: { color: "#10b981" },
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
      default: "FocusBot Deep Work",
      aligned: "FocusBot - Aligned",
      distracting: "FocusBot - Distracting",
      analyzing: "FocusBot - Analyzing",
      error: "FocusBot - Error"
    };

    const title =
      session?.pausedAt != null ? "FocusBot - Paused" : stateLabels[iconState];
    await chrome.action.setTitle({ title });

    if (session && isConnected()) {
      const summary = calculateLiveSummary(session);
      const state = getIntegrationState();
      const desktopCtx = state.currentDesktopContext;
      const cv = session.currentVisit;
      const onDesktop = Boolean(desktopCtx);
      sendFocusStatus({
        taskId: session.sessionId,
        classification: onDesktop ? desktopCtx!.classification : (cv?.classification ?? ""),
        reason: onDesktop ? desktopCtx!.reason : (cv?.reason ?? ""),
        score: onDesktop ? (desktopCtx!.classification === "aligned" ? 8 : 2) : (cv?.classification === "aligned" ? 8 : cv?.classification === "distracting" ? 2 : 0),
        focusScorePercent: summary.focusPercentage,
        contextType: onDesktop ? "desktop" : (cv ? "browser" : ""),
        contextTitle: onDesktop ? `${desktopCtx!.processName} - ${desktopCtx!.windowTitle}` : (cv?.domain ?? "")
      });
    }
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
  const domain = getDomain(taskUrl);
  const isExcluded = matchesExcludedDomain(domain, settings.excludedDomains);
  if (isExcluded) {
    return {
      ok: true,
      result: {
        classification: "aligned",
        confidence: 1,
        reason: "Domain is excluded from classifier checks."
      }
    };
  }

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
      latest.currentVisit = {
        ...latest.currentVisit,
        visitState: "classified",
        classification: outcome.result.classification,
        confidence: outcome.result.confidence,
        reason: outcome.result.reason
      };
      await saveLastError(null);
      await saveActiveSession(latest);
      await broadcastStateUpdate();

      if (isConnected()) {
        updateDesktopContext(undefined);
        sendFocusStatus({
          taskId: latest.sessionId,
          classification: outcome.result.classification,
          reason: outcome.result.reason ?? "",
          score: outcome.result.classification === "aligned" ? 8 : 2,
          focusScorePercent: calculateLiveSummary(latest).focusPercentage,
          contextType: "browser",
          contextTitle: latest.currentVisit.domain
        });
      }

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
    updateBrowserForeground(true);
    const session = await loadActiveSession();
    if (!session || tab.id === undefined || !tab.url || !isTrackableUrl(tab.url)) {
      return;
    }
    if (session.pausedAt) {
      return;
    }

    const title = tab.title ?? getDomain(tab.url);
    const domain = getDomain(tab.url);
    const current = session.currentVisit;
    const isSamePage =
      current &&
      current.tabId === tab.id &&
      current.url === tab.url &&
      current.title === title &&
      current.domain === domain;
    if (isSamePage) {
      return;
    }

    const transitionAt = nowIso();
    const sameTabSameDomainDistracting =
      current &&
      current.tabId === tab.id &&
      current.domain === domain &&
      current.visitState === "classified" &&
      current.classification === "distracting";

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

const pushBrowserContextToApp = async (): Promise<void> => {
  if (!isConnected()) return;
  try {
    const [activeTab] = await chrome.tabs.query({ active: true, lastFocusedWindow: true });
    if (activeTab?.url != null) {
      sendBrowserContext(activeTab.url, activeTab.title ?? "");
    } else {
      sendBrowserContext("", "");
    }
  } catch {
    sendBrowserContext("", "");
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

    const integrationState = getIntegrationState();
    if (integrationState.leaderTaskId && integrationState.connected) {
      return { ok: false, error: "A task is already in progress on the desktop app. End it there first." };
    }

    const settings = await loadSettings();
    if (!settings.openAiApiKey.trim()) {
      return { ok: false, error: "OpenAI API key is required. Add it in Settings first." };
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
    const latest = await loadActiveSession();
    await broadcastStateUpdate();

    if (isConnected()) {
      sendTaskStarted(session.sessionId, session.taskText, session.taskHints, session.startedAt);
      const summary = calculateLiveSummary(latest ?? session);
      sendFocusStatus({
        taskId: (latest ?? session).sessionId,
        classification: "",
        reason: "",
        score: 0,
        focusScorePercent: summary.focusPercentage,
        contextType: "",
        contextTitle: ""
      });
    }

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
    await broadcastStateUpdate();

    if (isConnected()) {
      sendTaskEnded(session.sessionId);
    }

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
    case "OPEN_ANALYTICS":
      await chrome.tabs.create({ url: chrome.runtime.getURL("src/analytics/index.html") });
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
      return { ok: true, data: getIntegrationState() };
    default:
      return { ok: false, error: "Unknown request." };
  }
};

const START_DESKTOP_INTEGRATION = "START_DESKTOP_INTEGRATION";
const FOCUSBOT_AUTH_SESSION_STORED = "FOCUSBOT_AUTH_SESSION_STORED";

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
    (message as { type: string }).type === START_DESKTOP_INTEGRATION
  ) {
    startIntegration();
    sendResponse({ ok: true });
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
      await patchSettings({
        authMode: "focusbot-account",
        focusbotEmail: email,
        onboardingCompleted: true
      });
      await broadcastStateUpdate();
    })
      .then(() => sendResponse({ ok: true }))
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
  await pushBrowserContextToApp();
});

chrome.tabs.onUpdated.addListener(async (_tabId, changeInfo, tab) => {
  if (!tab.active || (!changeInfo.url && changeInfo.status !== "complete")) {
    return;
  }
  await updateVisitFromTab(tab);
  await pushBrowserContextToApp();
});

chrome.windows.onFocusChanged.addListener(async (windowId) => {
  if (windowId === chrome.windows.WINDOW_ID_NONE) {
    return;
  }

  const [tab] = await chrome.tabs.query({ active: true, windowId });
  if (tab) {
    await updateVisitFromTab(tab);
    await pushBrowserContextToApp();
  }
});

chrome.alarms.onAlarm.addListener(async (alarm) => {
  if (alarm.name === BADGE_ALARM_NAME) {
    await updateIconState();
    await startBadgeInterval();
  }
});

chrome.runtime.onStartup.addListener(async () => {
  await migrateSessionsToCompletedSessions();
  await startBadgeInterval();
  await captureCurrentActiveTab();
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
});

chrome.idle.setDetectionInterval(IDLE_DETECTION_INTERVAL_SECONDS);

chrome.idle.onStateChanged.addListener((newState: chrome.IdleState) => {
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

import { calculateAnalytics } from "../shared/analytics";
import { classifyPage } from "../shared/classifier";
import { calculateSessionSummary, stripActiveSessionForHistory } from "../shared/metrics";
import {
  loadActiveSession,
  loadLastError,
  loadLastSummary,
  loadSessions,
  loadSettings,
  patchSettings,
  saveActiveSession,
  saveLastError,
  saveLastSummary,
  saveSession
} from "../shared/storage";
import type { ClassificationResult, FocusSession, InProgressVisit, RuntimeRequest, RuntimeResponse } from "../shared/types";
import { createId, nowIso, sleep } from "../shared/utils";
import { getDomain, isTrackableUrl, matchesExcludedDomain } from "../shared/url";
import { ICON_DATA_URLS, type IconState } from "../shared/types";

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

const getIconStateFromSession = (session: FocusSession | null): IconState => {
  console.log("getIconStateFromSession called with:", { session: session ? { sessionId: session.sessionId, currentVisit: session.currentVisit } : null });
  
  if (!session) {
    console.log("No session, returning default");
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

    console.log("Updating icon:", { iconState });

    const badgeConfig: Record<IconState, { text: string; color: string }> = {
      default: { text: "", color: "#6366f1" },
      aligned: { text: "✓", color: "#10b981" },
      distracting: { text: "✕", color: "#ef4444" },
      analyzing: { text: "…", color: "#a855f7" },
      error: { text: "!", color: "#a855f7" }
    };

    const config = badgeConfig[iconState];

    await chrome.action.setBadgeText({ text: config.text });
    await chrome.action.setBadgeBackgroundColor({ color: config.color });

    const stateLabels: Record<IconState, string> = {
      default: "FocusBot Deep Work",
      aligned: "FocusBot - Aligned",
      distracting: "FocusBot - Distracting",
      analyzing: "FocusBot - Analyzing",
      error: "FocusBot - Error"
    };

    await chrome.action.setTitle({
      title: stateLabels[iconState]
    });

    console.log("Icon updated successfully with state:", iconState);
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
        result: await classifyPage(settings, session.taskText, taskUrl, title, 8000)
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

      await sendDistractionAlert(tabId, {
        show: outcome.result.classification === "distracting",
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

const startSession = async (taskText: string): Promise<RuntimeResponse<FocusSession>> =>
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
    if (!settings.openAiApiKey.trim()) {
      return { ok: false, error: "OpenAI API key is required. Add it in Settings first." };
    }

    const session: FocusSession = {
      sessionId: createId(),
      taskText: trimmedTask,
      startedAt: nowIso(),
      visits: []
    };
    await saveLastError(null);
    await saveActiveSession(session);
    void captureCurrentActiveTab();
    const latest = await loadActiveSession();
    await broadcastStateUpdate();

    return { ok: true, data: latest ?? session };
  });

const endSession = async (): Promise<RuntimeResponse<FocusSession>> =>
  runExclusive(async () => {
    const session = await loadActiveSession();
    if (!session) {
      return { ok: false, error: "No active session to end." };
    }

    const endedAt = nowIso();
    if (session.currentVisit?.tabId !== undefined) {
      await sendDistractionAlert(session.currentVisit.tabId, { show: false });
    }
    finalizeCurrentVisit(session, endedAt);
    session.endedAt = endedAt;
    session.summary = calculateSessionSummary(session.taskText, session.startedAt, endedAt, session.visits);

    const sessionForHistory = stripActiveSessionForHistory(session);
    await saveSession(sessionForHistory);
    await saveLastSummary(session.summary);
    await saveActiveSession(null);
    await broadcastStateUpdate();

    return { ok: true, data: sessionForHistory };
  });

const handleRequest = async (request: RuntimeRequest): Promise<RuntimeResponse> => {
  switch (request.type) {
    case "GET_STATE":
      return { ok: true, data: await toRuntimeState() };
    case "START_SESSION":
      return startSession(request.taskText);
    case "END_SESSION":
      return endSession();
    case "GET_ANALYTICS": {
      const sessions = await loadSessions();
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
    default:
      return { ok: false, error: "Unknown request." };
  }
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
    return;
  }

  const [tab] = await chrome.tabs.query({ active: true, windowId });
  if (tab) {
    await updateVisitFromTab(tab);
  }
});

chrome.runtime.onStartup.addListener(async () => {
  await captureCurrentActiveTab();
});

chrome.runtime.onInstalled.addListener(async () => {
  await captureCurrentActiveTab();
});

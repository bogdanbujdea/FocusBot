import { calculateAnalytics } from "../shared/analytics";
import { classifyPage } from "../shared/classifier";
import { calculateSessionSummary, stripActiveSessionForHistory } from "../shared/metrics";
import {
  loadActiveSession,
  loadLastSummary,
  loadSessions,
  loadSettings,
  patchSettings,
  saveActiveSession,
  saveLastSummary,
  saveSession
} from "../shared/storage";
import type { ClassificationResult, FocusSession, InProgressVisit, RuntimeRequest, RuntimeResponse } from "../shared/types";
import { createId, nowIso } from "../shared/utils";
import { getDomain, isTrackableUrl, matchesExcludedDomain } from "../shared/url";

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
  lastSummary: await loadLastSummary()
});

const broadcastStateUpdate = async (): Promise<void> => {
  try {
    const state = await toRuntimeState();
    await chrome.runtime.sendMessage({ type: "STATE_UPDATED", data: state });
  } catch {
    // Ignore broadcast failures when no view is open.
  }
};

const finalizeCurrentVisit = (session: FocusSession, leftAt: string): void => {
  if (!session.currentVisit) {
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
    confidence: session.currentVisit.confidence,
    reason: session.currentVisit.reason
  });
  session.currentVisit = undefined;
};

const classifyWithFallback = async (
  session: FocusSession,
  taskUrl: string,
  title: string
): Promise<ClassificationResult> => {
  const settings = await loadSettings();
  const domain = getDomain(taskUrl);
  const isExcluded = matchesExcludedDomain(domain, settings.excludedDomains);
  if (isExcluded) {
    return {
      classification: "aligned",
      confidence: 1,
      reason: "Domain is excluded from classifier checks."
    };
  }

  try {
    return await classifyPage(settings, session.taskText, taskUrl, title);
  } catch (error) {
    return {
      classification: "aligned",
      confidence: 0,
      reason: error instanceof Error ? error.message : "Classification failed."
    };
  }
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
    finalizeCurrentVisit(session, transitionAt);

    const classification = await classifyWithFallback(session, tab.url, title);
    const nextVisit: InProgressVisit = {
      tabId: tab.id,
      url: tab.url,
      domain,
      title,
      enteredAt: transitionAt,
      classification: classification.classification,
      confidence: classification.confidence,
      reason: classification.reason
    };
    session.currentVisit = nextVisit;
    await saveActiveSession(session);
    await broadcastStateUpdate();
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
    await saveActiveSession(session);
    await captureCurrentActiveTab();
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
    case "OPEN_OPTIONS":
      await chrome.runtime.openOptionsPage();
      return { ok: true };
    case "OPEN_ANALYTICS":
      await chrome.tabs.create({ url: chrome.runtime.getURL("src/analytics/index.html") });
      return { ok: true };
    case "OPEN_SIDE_PANEL": {
      const currentWindow = await chrome.windows.getCurrent();
      if (!currentWindow.id) {
        return { ok: false, error: "Unable to open side panel in current window." };
      }
      await chrome.sidePanel.open({ windowId: currentWindow.id });
      return { ok: true };
    }
    default:
      return { ok: false, error: "Unknown request." };
  }
};

chrome.runtime.onMessage.addListener((message: RuntimeRequest, _sender, sendResponse) => {
  handleRequest(message)
    .then((response) => sendResponse(response))
    .catch((error: unknown) =>
      sendResponse({
        ok: false,
        error: error instanceof Error ? error.message : "Unhandled runtime error."
      } satisfies RuntimeResponse)
    );
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

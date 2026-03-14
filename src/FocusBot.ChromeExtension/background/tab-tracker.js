import { EVENT_TYPES } from '../shared/constants.js';
import { normalizeTabEvent, isSameContext } from './normalizer.js';
import { isTrackingEnabled, isUrlExcluded } from '../shared/storage.js';
import { sendActivityEvent } from './desktop-client.js';

let lastEmittedEvent = null;

export function setupTabListeners() {
  chrome.tabs.onActivated.addListener(onTabActivated);
  chrome.tabs.onUpdated.addListener(onTabUpdated);
  chrome.windows.onFocusChanged.addListener(onWindowFocusChanged);
}

async function onTabActivated(activeInfo) {
  if (!await isTrackingEnabled()) return;

  try {
    const tab = await chrome.tabs.get(activeInfo.tabId);
    if (tab.incognito) return;
    if (await isUrlExcluded(tab.url)) return;

    const event = normalizeTabEvent(tab, EVENT_TYPES.TAB_ACTIVATED);
    if (event) {
      lastEmittedEvent = event;
      await sendActivityEvent(event);
      notifyContentScript(tab.id, null);
    }
  } catch {
    // Tab may have been closed between event and get
  }
}

async function onTabUpdated(tabId, changeInfo, tab) {
  if (!changeInfo.url && !changeInfo.title) return;
  if (!await isTrackingEnabled()) return;
  if (!tab.active) return;
  if (tab.incognito) return;
  if (await isUrlExcluded(tab.url)) return;

  const event = normalizeTabEvent(tab, EVENT_TYPES.TAB_UPDATED);
  if (event && !isSameContext(event, lastEmittedEvent)) {
    lastEmittedEvent = event;
    await sendActivityEvent(event);
  }
}

async function onWindowFocusChanged(windowId) {
  if (!await isTrackingEnabled()) return;

  if (windowId === chrome.windows.WINDOW_ID_NONE) {
    const blurEvent = {
      eventType: EVENT_TYPES.WINDOW_BLURRED,
      browser: 'chrome',
      fullUrl: '',
      domain: '',
      path: '',
      title: '',
      tabId: 0,
      windowId: 0,
      occurredAtUtc: new Date().toISOString()
    };
    lastEmittedEvent = null;
    await sendActivityEvent(blurEvent);
    return;
  }

  try {
    const tabs = await chrome.tabs.query({ active: true, windowId: windowId });
    if (tabs.length === 0) return;

    const tab = tabs[0];
    if (tab.incognito) return;
    if (await isUrlExcluded(tab.url)) return;

    const event = normalizeTabEvent(tab, EVENT_TYPES.WINDOW_FOCUSED);
    if (event) {
      lastEmittedEvent = event;
      await sendActivityEvent(event);
    }
  } catch {
    // Window may have been closed
  }
}

export async function emitHeartbeat() {
  if (!await isTrackingEnabled()) return;

  try {
    const tabs = await chrome.tabs.query({ active: true, currentWindow: true });
    if (tabs.length === 0) return;

    const tab = tabs[0];
    if (tab.incognito) return;
    if (await isUrlExcluded(tab.url)) return;

    const event = normalizeTabEvent(tab, EVENT_TYPES.HEARTBEAT);
    if (event) {
      lastEmittedEvent = event;
      await sendActivityEvent(event);
    }
  } catch {
    // No active tab available
  }
}

function notifyContentScript(tabId, focusState) {
  chrome.tabs.sendMessage(tabId, {
    type: 'focusStateUpdate',
    data: focusState
  }).catch(() => {
    // Content script may not be loaded yet
  });
}

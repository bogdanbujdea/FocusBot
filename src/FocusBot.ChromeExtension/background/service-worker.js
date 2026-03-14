import { setupTabListeners, emitHeartbeat } from './tab-tracker.js';
import { fetchFocusState, getConnectionStatus } from './desktop-client.js';
import { isTrackingEnabled, isOverlayEnabled } from '../shared/storage.js';
import { HEARTBEAT_INTERVAL_MS, FOCUS_POLL_INTERVAL_MS } from '../shared/constants.js';

let heartbeatTimer = null;
let focusPollTimer = null;
let lastFocusState = null;

setupTabListeners();
startTimers();

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message.type === 'getFocusState') {
    const state = lastFocusState || { status: 'unknown', connected: getConnectionStatus() };
    sendResponse(state);
    return true;
  }

  if (message.type === 'getConnectionStatus') {
    sendResponse({ connected: getConnectionStatus() });
    return true;
  }

  if (message.type === 'settingsChanged') {
    restartTimers();
    sendResponse({ ok: true });
    return true;
  }

  return false;
});

function startTimers() {
  stopTimers();

  heartbeatTimer = setInterval(async () => {
    if (await isTrackingEnabled()) {
      await emitHeartbeat();
    }
  }, HEARTBEAT_INTERVAL_MS);

  focusPollTimer = setInterval(async () => {
    await pollFocusState();
  }, FOCUS_POLL_INTERVAL_MS);
}

function stopTimers() {
  if (heartbeatTimer) {
    clearInterval(heartbeatTimer);
    heartbeatTimer = null;
  }
  if (focusPollTimer) {
    clearInterval(focusPollTimer);
    focusPollTimer = null;
  }
}

function restartTimers() {
  startTimers();
}

async function pollFocusState() {
  const state = await fetchFocusState();
  lastFocusState = state;

  if (!await isOverlayEnabled()) return;

  try {
    const tabs = await chrome.tabs.query({ active: true, currentWindow: true });
    if (tabs.length === 0) return;

    const tab = tabs[0];
    if (tab.incognito) return;

    chrome.tabs.sendMessage(tab.id, {
      type: 'focusStateUpdate',
      data: state
    }).catch(() => {});
  } catch {
    // No active tab
  }
}

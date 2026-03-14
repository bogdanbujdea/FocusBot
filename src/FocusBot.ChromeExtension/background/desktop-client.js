import {
  ACTIVITY_ENDPOINT,
  FOCUS_STATE_ENDPOINT,
  EVENT_BUFFER_MAX_SIZE
} from '../shared/constants.js';

let isConnected = false;
let eventBuffer = [];

export function getConnectionStatus() {
  return isConnected;
}

export async function sendActivityEvent(event) {
  if (!event) return;

  try {
    const response = await fetch(ACTIVITY_ENDPOINT, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(event),
      signal: AbortSignal.timeout(5000)
    });

    if (response.ok) {
      isConnected = true;
      flushBuffer();
      return true;
    }

    isConnected = false;
    bufferEvent(event);
    return false;
  } catch {
    isConnected = false;
    bufferEvent(event);
    return false;
  }
}

export async function fetchFocusState() {
  try {
    const response = await fetch(FOCUS_STATE_ENDPOINT, {
      method: 'GET',
      headers: { 'Accept': 'application/json' },
      signal: AbortSignal.timeout(5000)
    });

    if (response.ok) {
      isConnected = true;
      const state = await response.json();
      state.connected = true;
      return state;
    }

    isConnected = false;
    return { status: 'disconnected', connected: false };
  } catch {
    isConnected = false;
    return { status: 'disconnected', connected: false };
  }
}

function bufferEvent(event) {
  eventBuffer.push(event);
  if (eventBuffer.length > EVENT_BUFFER_MAX_SIZE) {
    eventBuffer = eventBuffer.slice(-EVENT_BUFFER_MAX_SIZE);
  }
}

async function flushBuffer() {
  if (eventBuffer.length === 0) return;

  const events = [...eventBuffer];
  eventBuffer = [];

  for (const event of events) {
    try {
      await fetch(ACTIVITY_ENDPOINT, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(event),
        signal: AbortSignal.timeout(3000)
      });
    } catch {
      break;
    }
  }
}

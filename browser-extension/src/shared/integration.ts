import type {
  IntegrationEnvelope,
  IntegrationState,
  HandshakePayload,
  TaskStartedPayload,
  FocusStatusPayload,
  DesktopForegroundPayload,
  DesktopClassificationResult,
  BrowserContextPayload
} from "./integrationTypes";
import { MESSAGE_TYPES } from "./integrationTypes";

const WS_URL = "ws://localhost:9876/focusbot";
const RECONNECT_INTERVAL_MS = 5000;

type MessageHandler = (envelope: IntegrationEnvelope) => void;

interface HandshakeInfo {
  hasActiveTask: boolean;
  taskId?: string;
  taskText?: string;
  taskHints?: string;
  startedAt?: string;
}

type HandshakeProvider = () => Promise<HandshakeInfo>;

let ws: WebSocket | null = null;
let reconnectTimer: ReturnType<typeof setTimeout> | null = null;
let messageHandler: MessageHandler | null = null;
let handshakeProvider: HandshakeProvider | null = null;
let shouldConnect = true;

const state: IntegrationState = {
  connected: false,
  browserInForeground: true
};

const stateListeners: Array<(state: IntegrationState) => void> = [];

const notifyStateChange = (): void => {
  const snapshot = { ...state };
  for (const listener of stateListeners) {
    try {
      listener(snapshot);
    } catch {
      // listener errors should not crash integration
    }
  }
};

export const onIntegrationStateChange = (listener: (state: IntegrationState) => void): (() => void) => {
  stateListeners.push(listener);
  return () => {
    const idx = stateListeners.indexOf(listener);
    if (idx >= 0) stateListeners.splice(idx, 1);
  };
};

export const getIntegrationState = (): IntegrationState => ({ ...state });

export const updateLeaderTask = (taskId: string, taskText: string): void => {
  state.leaderTaskId = taskId;
  state.leaderTaskText = taskText;
  notifyStateChange();
};

export const clearLeaderTask = (): void => {
  state.leaderTaskId = undefined;
  state.leaderTaskText = undefined;
  state.lastFocusStatus = undefined;
  notifyStateChange();
};

export const updateLastFocusStatus = (status: FocusStatusPayload): void => {
  state.lastFocusStatus = status;
  notifyStateChange();
};

export const updateDesktopContext = (context: DesktopClassificationResult | undefined): void => {
  state.currentDesktopContext = context;
  notifyStateChange();
};

export const updateBrowserForeground = (inForeground: boolean): void => {
  if (state.browserInForeground === inForeground) return;
  state.browserInForeground = inForeground;
  notifyStateChange();
};

const scheduleReconnect = (): void => {
  if (!shouldConnect) return;
  console.info("[Foqus] Desktop app not available; will retry in 5s.");
  reconnectTimer = setTimeout(() => connect(), RECONNECT_INTERVAL_MS);
};

const connect = (): void => {
  if (ws?.readyState === WebSocket.OPEN || ws?.readyState === WebSocket.CONNECTING) return;

  try {
    ws = new WebSocket(WS_URL);
  } catch {
    scheduleReconnect();
    return;
  }

  ws.onopen = async () => {
    console.info("[Foqus] Connected to desktop app.");
    state.connected = true;
    notifyStateChange();

    if (handshakeProvider) {
      const info = await handshakeProvider();
      sendHandshake(info.hasActiveTask, info.taskId, info.taskText, info.taskHints, info.startedAt);
    }
  };

  ws.onclose = () => {
    console.info("[Foqus] Desktop app disconnected.");
    state.connected = false;
    clearLeaderTask();
    state.browserInForeground = true;
    notifyStateChange();
    ws = null;
    // Always retry when we want to be connected (not only after a previous success),
    // so that when the user starts the Windows app we connect within one interval.
    scheduleReconnect();
  };

  ws.onerror = () => {
    console.info("[Foqus] Desktop app connection failed (optional; will retry).");
  };

  ws.onmessage = (event) => {
    try {
      const envelope = JSON.parse(event.data as string) as IntegrationEnvelope;
      messageHandler?.(envelope);
    } catch (err) {
      console.error("[Integration] Failed to parse message:", err);
    }
  };
};

export const sendMessage = (type: string, payload?: unknown): void => {
  if (!ws || ws.readyState !== WebSocket.OPEN) return;
  const envelope: IntegrationEnvelope = { type, payload };
  ws.send(JSON.stringify(envelope));
};

export const sendHandshake = (hasActiveTask: boolean, taskId?: string, taskText?: string, taskHints?: string, startedAt?: string): void => {
  const payload: HandshakePayload = {
    source: "extension",
    hasActiveTask,
    taskId,
    taskText,
    taskHints,
    startedAt
  };
  sendMessage(MESSAGE_TYPES.HANDSHAKE, payload);
};

export const sendTaskStarted = (taskId: string, taskText: string, taskHints?: string, startedAt?: string): void => {
  const payload: TaskStartedPayload = { taskId, taskText, taskHints, startedAt };
  sendMessage(MESSAGE_TYPES.TASK_STARTED, payload);
};

export const sendTaskEnded = (taskId: string): void => {
  const payload = { taskId };
  sendMessage(MESSAGE_TYPES.TASK_ENDED, payload);
};

export const sendFocusStatus = (payload: FocusStatusPayload): void => {
  sendMessage(MESSAGE_TYPES.FOCUS_STATUS, payload);
};

export const sendBrowserContext = (url: string, title: string): void => {
  const payload: BrowserContextPayload = { url, title };
  sendMessage(MESSAGE_TYPES.BROWSER_CONTEXT, payload);
};

export const setMessageHandler = (handler: MessageHandler): void => {
  messageHandler = handler;
};

export const setHandshakeProvider = (provider: HandshakeProvider): void => {
  handshakeProvider = provider;
};

export const startIntegration = (): void => {
  shouldConnect = true;
  connect();
};

export const stopIntegration = (): void => {
  shouldConnect = false;
  if (reconnectTimer) {
    clearTimeout(reconnectTimer);
    reconnectTimer = null;
  }
  if (ws) {
    ws.onclose = null;
    ws.close();
    ws = null;
  }
  state.connected = false;
  clearLeaderTask();
  state.browserInForeground = true;
  notifyStateChange();
};

export const isConnected = (): boolean => ws?.readyState === WebSocket.OPEN;

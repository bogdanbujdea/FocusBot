import type {
  IntegrationEnvelope,
  IntegrationMode,
  IntegrationState,
  HandshakePayload,
  TaskStartedPayload,
  FocusStatusPayload,
  DesktopForegroundPayload,
  DesktopClassificationResult,
  RequestBrowserUrlPayload,
  BrowserUrlResponsePayload
} from "./integrationTypes";
import { MESSAGE_TYPES } from "./integrationTypes";

const WS_URL = "ws://localhost:9876/focusbot";
const RECONNECT_BASE_MS = 1000;
const RECONNECT_MAX_MS = 30000;

type MessageHandler = (envelope: IntegrationEnvelope) => void;

interface HandshakeInfo {
  hasActiveTask: boolean;
  taskId?: string;
  taskText?: string;
  taskHints?: string;
}

type HandshakeProvider = () => Promise<HandshakeInfo>;

let ws: WebSocket | null = null;
let reconnectAttempt = 0;
let reconnectTimer: ReturnType<typeof setTimeout> | null = null;
let messageHandler: MessageHandler | null = null;
let handshakeProvider: HandshakeProvider | null = null;
let shouldConnect = true;

const state: IntegrationState = {
  mode: "standalone",
  connected: false
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

export const setMode = (mode: IntegrationMode): void => {
  if (state.mode === mode) return;
  state.mode = mode;
  notifyStateChange();
};

export const updateLeaderTask = (taskId: string, taskText: string): void => {
  state.leaderTaskId = taskId;
  state.leaderTaskText = taskText;
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

const scheduleReconnect = (): void => {
  if (!shouldConnect) return;
  const delay = Math.min(RECONNECT_BASE_MS * Math.pow(2, reconnectAttempt), RECONNECT_MAX_MS);
  reconnectAttempt++;
  console.log(`[Integration] Reconnecting in ${delay}ms (attempt ${reconnectAttempt})`);
  reconnectTimer = setTimeout(() => connect(), delay);
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
    console.log("[Integration] Connected to app");
    reconnectAttempt = 0;
    state.connected = true;
    notifyStateChange();

    if (handshakeProvider) {
      const info = await handshakeProvider();
      sendHandshake(info.hasActiveTask, info.taskId, info.taskText, info.taskHints);
    }
  };

  ws.onclose = () => {
    console.log("[Integration] Disconnected from app");
    state.connected = false;
    if (state.mode === "companionMode") {
      state.mode = "standalone";
      state.leaderTaskId = undefined;
      state.leaderTaskText = undefined;
      state.lastFocusStatus = undefined;
    }
    notifyStateChange();
    ws = null;
    scheduleReconnect();
  };

  ws.onerror = () => {
    // onclose will fire after onerror
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

export const sendHandshake = (hasActiveTask: boolean, taskId?: string, taskText?: string, taskHints?: string): void => {
  const payload: HandshakePayload = {
    source: "extension",
    hasActiveTask,
    taskId,
    taskText,
    taskHints
  };
  sendMessage(MESSAGE_TYPES.HANDSHAKE, payload);
};

export const sendTaskStarted = (taskId: string, taskText: string, taskHints?: string): void => {
  setMode("fullMode");
  const payload: TaskStartedPayload = { taskId, taskText, taskHints };
  sendMessage(MESSAGE_TYPES.TASK_STARTED, payload);
};

export const sendTaskEnded = (taskId: string): void => {
  const payload = { taskId };
  sendMessage(MESSAGE_TYPES.TASK_ENDED, payload);
  setMode("standalone");
};

export const sendFocusStatus = (payload: FocusStatusPayload): void => {
  sendMessage(MESSAGE_TYPES.FOCUS_STATUS, payload);
};

export const sendBrowserUrlResponse = (requestId: string, url: string, title: string): void => {
  const payload: BrowserUrlResponsePayload = { requestId, url, title };
  sendMessage(MESSAGE_TYPES.BROWSER_URL_RESPONSE, payload);
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
  state.mode = "standalone";
  notifyStateChange();
};

export const isConnected = (): boolean => ws?.readyState === WebSocket.OPEN;

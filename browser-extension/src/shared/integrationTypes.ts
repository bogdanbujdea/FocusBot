export type IntegrationMode = "standalone" | "fullMode" | "companionMode";

export interface IntegrationEnvelope {
  type: string;
  payload?: unknown;
}

export interface HandshakePayload {
  source: string;
  hasActiveTask: boolean;
  taskId?: string;
  taskText?: string;
  taskHints?: string;
}

export interface TaskStartedPayload {
  taskId: string;
  taskText: string;
  taskHints?: string;
}

export interface TaskEndedPayload {
  taskId: string;
}

export interface FocusStatusPayload {
  taskId: string;
  classification: string;
  reason: string;
  score: number;
  focusScorePercent: number;
  contextType: string;
  contextTitle: string;
}

export interface DesktopForegroundPayload {
  processName: string;
  windowTitle: string;
}

export interface RequestBrowserUrlPayload {
  requestId: string;
}

export interface BrowserUrlResponsePayload {
  requestId: string;
  url: string;
  title: string;
}

export interface DesktopClassificationResult {
  processName: string;
  windowTitle: string;
  classification: string;
  reason: string;
  timestamp: number;
}

export interface IntegrationState {
  mode: IntegrationMode;
  connected: boolean;
  browserInForeground: boolean;
  leaderTaskId?: string;
  leaderTaskText?: string;
  lastFocusStatus?: FocusStatusPayload;
  currentDesktopContext?: DesktopClassificationResult;
}

export const MESSAGE_TYPES = {
  HANDSHAKE: "HANDSHAKE",
  TASK_STARTED: "TASK_STARTED",
  TASK_ENDED: "TASK_ENDED",
  FOCUS_STATUS: "FOCUS_STATUS",
  DESKTOP_FOREGROUND: "DESKTOP_FOREGROUND",
  REQUEST_BROWSER_URL: "REQUEST_BROWSER_URL",
  BROWSER_URL_RESPONSE: "BROWSER_URL_RESPONSE"
} as const;

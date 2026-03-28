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
  startedAt?: string;
}

export interface TaskStartedPayload {
  taskId: string;
  taskText: string;
  taskHints?: string;
  startedAt?: string;
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

export interface BrowserContextPayload {
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
  BROWSER_CONTEXT: "BROWSER_CONTEXT"
} as const;

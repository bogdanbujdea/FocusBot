import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
  type ILogger
} from "@microsoft/signalr";
import { getApiBaseUrl } from "./apiClient";
import { loadFocusbotAuthSession } from "./focusbotAuth";

const logPrefix = "[SignalR][focus][extension]";
let connection: HubConnection | null = null;

/** Latest callbacks so reconnecting is not required when handlers are updated. */
let hubCallbacks: FocusHubCallbacks = {};

const signalRLogger: ILogger = {
  log(level: LogLevel, message: string): void {
    if (level >= LogLevel.Warning) {
      console.warn(`${logPrefix} ${message}`);
    }
  }
};

export interface SessionStartedEvent {
  sessionId: string;
  sessionTitle: string;
  sessionContext?: string;
  startedAtUtc: string;
  source: string;
}

export interface SessionEndedEvent {
  sessionId: string;
  endedAtUtc: string;
  source: string;
}

export interface SessionPausedEvent {
  sessionId: string;
  pausedAtUtc: string;
  source: string;
}

export interface SessionResumedEvent {
  sessionId: string;
  source: string;
}

export interface ClassificationChangedEvent {
  score: number;
  reason: string;
  source: string;
  activityName: string;
  classifiedAtUtc: string;
  cached: boolean;
}

export type FocusHubCallbacks = {
  onSessionStarted?: (e: SessionStartedEvent) => void;
  onSessionEnded?: (e: SessionEndedEvent) => void;
  onSessionPaused?: (e: SessionPausedEvent) => void;
  onSessionResumed?: (e: SessionResumedEvent) => void;
  onClassificationChanged?: (e: ClassificationChangedEvent) => void;
  onReconnected?: () => void;
};

export const connectFocusHub = async (callbacks: FocusHubCallbacks): Promise<void> => {
  hubCallbacks = callbacks;

  if (connection && connection.state === HubConnectionState.Connected) {
    return;
  }

  if (connection) {
    try {
      await connection.stop();
    } catch {
      // Ignore teardown issues while replacing stale connections.
    }
    connection = null;
  }

  connection = new HubConnectionBuilder()
    .withUrl(`${getApiBaseUrl()}/hubs/focus`, {
      accessTokenFactory: async () => {
        const session = await loadFocusbotAuthSession();
        return session?.accessToken ?? "";
      }
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(signalRLogger)
    .build();

  connection.onreconnecting((error) => {
    console.warn(`${logPrefix} reconnecting`, error);
  });

  connection.onreconnected((connectionId) => {
    console.info(`${logPrefix} reconnected (connectionId=${connectionId ?? "none"})`);
    hubCallbacks.onReconnected?.();
  });

  connection.onclose((error) => {
    console.warn(`${logPrefix} connection closed`, error);
  });

  connection.on("SessionStarted", (e) => {
    hubCallbacks.onSessionStarted?.(e);
  });
  connection.on("SessionEnded", (e) => {
    hubCallbacks.onSessionEnded?.(e);
  });
  connection.on("SessionPaused", (e) => {
    hubCallbacks.onSessionPaused?.(e);
  });
  connection.on("SessionResumed", (e) => {
    hubCallbacks.onSessionResumed?.(e);
  });
  connection.on("ClassificationChanged", (e) => {
    hubCallbacks.onClassificationChanged?.(e);
  });

  try {
    await connection.start();
  } catch (error) {
    const msg = error instanceof Error ? error.message : String(error);
    if (msg.includes("stopped during negotiation")) return;
    console.warn(`${logPrefix} failed to connect`, {
      error,
      message: msg,
      state: connection?.state ?? "none"
    });
  }
};

export const disconnectFocusHub = async (): Promise<void> => {
  if (!connection) return;
  try {
    await connection.stop();
  } catch (error) {
    console.warn(`${logPrefix} connection stop failed`, error);
  } finally {
    connection = null;
  }
};

export const getFocusHubState = (): HubConnectionState | null => connection?.state ?? null;

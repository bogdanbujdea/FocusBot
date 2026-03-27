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

export type FocusHubCallbacks = {
  onSessionStarted?: (e: SessionStartedEvent) => void;
  onSessionEnded?: (e: SessionEndedEvent) => void;
  onSessionPaused?: (e: SessionPausedEvent) => void;
  onSessionResumed?: (e: SessionResumedEvent) => void;
  onReconnected?: () => void;
};

export const connectFocusHub = async (callbacks: FocusHubCallbacks): Promise<void> => {
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
    callbacks.onReconnected?.();
  });

  connection.onclose((error) => {
    console.warn(`${logPrefix} connection closed`, error);
  });

  if (callbacks.onSessionStarted) {
    connection.on("SessionStarted", callbacks.onSessionStarted);
  }
  if (callbacks.onSessionEnded) {
    connection.on("SessionEnded", callbacks.onSessionEnded);
  }
  if (callbacks.onSessionPaused) {
    connection.on("SessionPaused", callbacks.onSessionPaused);
  }
  if (callbacks.onSessionResumed) {
    connection.on("SessionResumed", callbacks.onSessionResumed);
  }

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

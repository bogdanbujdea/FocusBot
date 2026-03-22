import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
  type ILogger,
} from "@microsoft/signalr";
import { supabase } from "../auth/supabase";

const API_BASE_URL =
  (import.meta.env.VITE_API_BASE_URL as string | undefined)?.trim() ||
  (import.meta.env.DEV ? "http://localhost:5251" : "https://api.foqus.me");

let connection: HubConnection | null = null;

// Custom logger that swallows the "stopped during negotiation" noise that
// React Strict Mode generates by unmounting/remounting effects in development.
// Real connection failures are still forwarded to console.warn.
const signalRLogger: ILogger = {
  log(level: LogLevel, message: string): void {
    if (message.includes("stopped during negotiation")) return;
    if (level >= LogLevel.Warning) {
      // eslint-disable-next-line no-console
      console.warn(`[SignalR] ${message}`);
    }
  },
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
};

export async function connectFocusHub(
  callbacks: FocusHubCallbacks
): Promise<void> {
  if (
    connection &&
    connection.state !== HubConnectionState.Disconnected
  ) {
    return;
  }

  connection = new HubConnectionBuilder()
    .withUrl(`${API_BASE_URL}/hubs/focus`, {
      accessTokenFactory: async () => {
        const {
          data: { session },
        } = await supabase.auth.getSession();
        return session?.access_token ?? "";
      },
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(signalRLogger)
    .build();

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
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err);
    if (msg.includes("stopped during negotiation")) return;
    console.warn("[SignalR] Failed to connect:", err);
  }
}

export async function disconnectFocusHub(): Promise<void> {
  if (connection) {
    try {
      await connection.stop();
    } catch {
      // Ignore stop errors during teardown
    }
    connection = null;
  }
}

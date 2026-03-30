const PRIMARY_PORT = 9876;
const BACKUP_PORT = 9877;
const PATH = "/foqus-presence";
const PING_INTERVAL_MS = 30000;
const RECONNECT_INTERVAL_MS = 5000;

interface PresenceMessage {
  type: string;
}

let socket: WebSocket | null = null;
let pingInterval: ReturnType<typeof setInterval> | null = null;
let reconnectTimeout: ReturnType<typeof setTimeout> | null = null;
let currentPort = PRIMARY_PORT;
let isConnecting = false;

export const startExtensionPresence = (): void => {
  if (socket?.readyState === WebSocket.OPEN || isConnecting) {
    return;
  }
  connectPresence();
};

export const stopExtensionPresence = (): void => {
  if (pingInterval) {
    clearInterval(pingInterval);
    pingInterval = null;
  }
  if (reconnectTimeout) {
    clearTimeout(reconnectTimeout);
    reconnectTimeout = null;
  }
  if (socket) {
    socket.close();
    socket = null;
  }
  isConnecting = false;
};

const connectPresence = (): void => {
  if (isConnecting) return;
  isConnecting = true;

  const url = `ws://localhost:${currentPort}${PATH}`;
  console.info(`[Foqus Presence] Attempting to connect to ${url}`);
  
  try {
    socket = new WebSocket(url);

    socket.onopen = () => {
      console.info(`[Foqus Presence] ✓ Connected to desktop app on port ${currentPort}`);
      isConnecting = false;
      
      if (pingInterval) clearInterval(pingInterval);
      pingInterval = setInterval(() => {
        if (socket?.readyState === WebSocket.OPEN) {
          const ping: PresenceMessage = { type: "ping" };
          socket.send(JSON.stringify(ping));
        }
      }, PING_INTERVAL_MS);

      const ping: PresenceMessage = { type: "ping" };
      socket.send(JSON.stringify(ping));
    };

    socket.onclose = () => {
      console.info("[Foqus Presence] Disconnected from desktop app");
      isConnecting = false;
      if (pingInterval) {
        clearInterval(pingInterval);
        pingInterval = null;
      }
      socket = null;
      scheduleReconnect();
    };

    socket.onerror = (error) => {
      console.warn("[Foqus Presence] Connection error:", error);
      isConnecting = false;
      
      if (currentPort === PRIMARY_PORT) {
        currentPort = BACKUP_PORT;
        console.info(`[Foqus Presence] Trying backup port ${BACKUP_PORT}`);
      } else {
        currentPort = PRIMARY_PORT;
      }
      
      socket?.close();
    };

    socket.onmessage = (event) => {
      try {
        const message = JSON.parse(event.data) as PresenceMessage;
        if (message.type === "pong") {
        }
      } catch {
      }
    };
  } catch (error) {
    console.warn("[Foqus Presence] Failed to create WebSocket:", error);
    isConnecting = false;
    scheduleReconnect();
  }
};

const scheduleReconnect = (): void => {
  if (reconnectTimeout) return;
  reconnectTimeout = setTimeout(() => {
    reconnectTimeout = null;
    connectPresence();
  }, RECONNECT_INTERVAL_MS);
};

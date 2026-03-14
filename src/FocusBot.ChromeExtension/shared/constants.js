export const DESKTOP_PORT = 51789;
export const DESKTOP_BASE_URL = `http://localhost:${DESKTOP_PORT}`;
export const ACTIVITY_ENDPOINT = `${DESKTOP_BASE_URL}/api/browser-activity`;
export const FOCUS_STATE_ENDPOINT = `${DESKTOP_BASE_URL}/api/focus-state`;

export const HEARTBEAT_INTERVAL_MS = 12000;
export const FOCUS_POLL_INTERVAL_MS = 3000;
export const CONNECTION_RETRY_INTERVAL_MS = 10000;
export const EVENT_BUFFER_MAX_SIZE = 50;

export const DEFAULT_EXCLUDED_DOMAINS = [
  'chase.com',
  'bankofamerica.com',
  'wellsfargo.com',
  'paypal.com',
  'venmo.com',
  'mint.com',
  'capitalone.com',
  'citibank.com',
  'usbank.com',
  'schwab.com',
  'fidelity.com',
  'vanguard.com',
  'robinhood.com',
  'coinbase.com',
  'binance.com',
  'stripe.com',
  'chrome://',
  'chrome-extension://',
  'edge://',
  'about:',
  'devtools://'
];

export const EVENT_TYPES = {
  TAB_ACTIVATED: 'tab_activated',
  TAB_UPDATED: 'tab_updated',
  WINDOW_FOCUSED: 'window_focused',
  WINDOW_BLURRED: 'window_blurred',
  HEARTBEAT: 'heartbeat'
};

export const STORAGE_KEYS = {
  TRACKING_ENABLED: 'trackingEnabled',
  OVERLAY_ENABLED: 'overlayEnabled',
  EXCLUDED_DOMAINS: 'excludedDomains',
  PAUSE_UNTIL: 'pauseUntil'
};

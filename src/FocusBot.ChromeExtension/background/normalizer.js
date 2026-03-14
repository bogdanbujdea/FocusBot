import { EVENT_TYPES } from '../shared/constants.js';

const INTERNAL_URL_PREFIXES = [
  'chrome://', 'chrome-extension://', 'edge://', 'about:', 'devtools://',
  'chrome-search://', 'chrome-devtools://'
];

export function normalizeTabEvent(tab, eventType) {
  if (!tab || !tab.url) return null;

  const url = tab.url;
  if (isInternalUrl(url)) return null;
  if (!url.startsWith('http://') && !url.startsWith('https://')) return null;

  let parsed;
  try {
    parsed = new URL(url);
  } catch {
    return null;
  }

  return {
    eventType: eventType,
    browser: 'chrome',
    fullUrl: url,
    domain: parsed.hostname,
    path: parsed.pathname,
    title: (tab.title || '').substring(0, 500),
    tabId: tab.id || 0,
    windowId: tab.windowId || 0,
    occurredAtUtc: new Date().toISOString()
  };
}

export function isInternalUrl(url) {
  if (!url) return true;
  return INTERNAL_URL_PREFIXES.some(prefix => url.startsWith(prefix));
}

export function isSameContext(eventA, eventB) {
  if (!eventA || !eventB) return false;
  return eventA.domain === eventB.domain
    && eventA.path === eventB.path
    && eventA.tabId === eventB.tabId;
}

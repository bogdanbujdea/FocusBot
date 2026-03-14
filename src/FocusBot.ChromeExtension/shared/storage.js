import { DEFAULT_EXCLUDED_DOMAINS, STORAGE_KEYS } from './constants.js';

export async function getSettings() {
  const defaults = {
    [STORAGE_KEYS.TRACKING_ENABLED]: true,
    [STORAGE_KEYS.OVERLAY_ENABLED]: true,
    [STORAGE_KEYS.EXCLUDED_DOMAINS]: DEFAULT_EXCLUDED_DOMAINS,
    [STORAGE_KEYS.PAUSE_UNTIL]: 0
  };

  const stored = await chrome.storage.local.get(defaults);
  return stored;
}

export async function getSetting(key) {
  const settings = await getSettings();
  return settings[key];
}

export async function setSetting(key, value) {
  await chrome.storage.local.set({ [key]: value });
}

export async function isTrackingEnabled() {
  const settings = await getSettings();
  if (!settings[STORAGE_KEYS.TRACKING_ENABLED]) return false;
  const pauseUntil = settings[STORAGE_KEYS.PAUSE_UNTIL] || 0;
  if (pauseUntil > 0 && Date.now() < pauseUntil) return false;
  return true;
}

export async function isOverlayEnabled() {
  return await getSetting(STORAGE_KEYS.OVERLAY_ENABLED);
}

export async function getExcludedDomains() {
  return await getSetting(STORAGE_KEYS.EXCLUDED_DOMAINS) || [];
}

export async function isDomainExcluded(domain) {
  if (!domain) return true;
  const excluded = await getExcludedDomains();
  const lowerDomain = domain.toLowerCase();
  return excluded.some(d => {
    const lowerExcluded = d.toLowerCase();
    return lowerDomain === lowerExcluded || lowerDomain.endsWith('.' + lowerExcluded);
  });
}

export async function isUrlExcluded(url) {
  if (!url) return true;
  const excluded = await getExcludedDomains();
  const lowerUrl = url.toLowerCase();
  for (const pattern of excluded) {
    if (lowerUrl.startsWith(pattern.toLowerCase())) return true;
  }
  try {
    const parsed = new URL(url);
    return await isDomainExcluded(parsed.hostname);
  } catch {
    return true;
  }
}

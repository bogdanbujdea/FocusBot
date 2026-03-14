import { STORAGE_KEYS, DEFAULT_EXCLUDED_DOMAINS } from '../shared/constants.js';
import { getSettings, setSetting, getExcludedDomains } from '../shared/storage.js';

const trackingToggle = document.getElementById('trackingEnabled');
const overlayToggle = document.getElementById('overlayEnabled');
const connectionStatus = document.getElementById('connectionStatus');
const domainList = document.getElementById('domainList');
const newDomainInput = document.getElementById('newDomain');
const addDomainBtn = document.getElementById('addDomain');
const pause15Btn = document.getElementById('pause15');
const pause30Btn = document.getElementById('pause30');
const pause60Btn = document.getElementById('pause60');
const pauseResumeBtn = document.getElementById('pauseResume');
const pauseStatusEl = document.getElementById('pauseStatus');

async function init() {
  const settings = await getSettings();

  trackingToggle.checked = settings[STORAGE_KEYS.TRACKING_ENABLED];
  overlayToggle.checked = settings[STORAGE_KEYS.OVERLAY_ENABLED];

  trackingToggle.addEventListener('change', () => {
    setSetting(STORAGE_KEYS.TRACKING_ENABLED, trackingToggle.checked);
    notifySettingsChanged();
  });

  overlayToggle.addEventListener('change', () => {
    setSetting(STORAGE_KEYS.OVERLAY_ENABLED, overlayToggle.checked);
    notifySettingsChanged();
  });

  addDomainBtn.addEventListener('click', addDomain);
  newDomainInput.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') addDomain();
  });

  pause15Btn.addEventListener('click', () => pauseTracking(15));
  pause30Btn.addEventListener('click', () => pauseTracking(30));
  pause60Btn.addEventListener('click', () => pauseTracking(60));
  pauseResumeBtn.addEventListener('click', resumeTracking);

  await renderDomainList();
  checkConnection();
  updatePauseStatus(settings[STORAGE_KEYS.PAUSE_UNTIL] || 0);

  setInterval(checkConnection, 5000);
  setInterval(async () => {
    const s = await getSettings();
    updatePauseStatus(s[STORAGE_KEYS.PAUSE_UNTIL] || 0);
  }, 1000);
}

async function addDomain() {
  const domain = newDomainInput.value.trim().toLowerCase();
  if (!domain) return;

  const domains = await getExcludedDomains();
  if (!domains.includes(domain)) {
    domains.push(domain);
    await setSetting(STORAGE_KEYS.EXCLUDED_DOMAINS, domains);
  }

  newDomainInput.value = '';
  await renderDomainList();
}

async function removeDomain(domain) {
  const domains = await getExcludedDomains();
  const updated = domains.filter(d => d !== domain);
  await setSetting(STORAGE_KEYS.EXCLUDED_DOMAINS, updated);
  await renderDomainList();
}

async function renderDomainList() {
  const domains = await getExcludedDomains();
  domainList.innerHTML = '';

  for (const domain of domains) {
    const tag = document.createElement('span');
    tag.className = 'domain-tag';
    tag.innerHTML = `${escapeHtml(domain)} <span class="remove" data-domain="${escapeHtml(domain)}">&times;</span>`;
    domainList.appendChild(tag);
  }

  domainList.querySelectorAll('.remove').forEach(el => {
    el.addEventListener('click', () => removeDomain(el.dataset.domain));
  });
}

async function pauseTracking(minutes) {
  const pauseUntil = Date.now() + (minutes * 60 * 1000);
  await setSetting(STORAGE_KEYS.PAUSE_UNTIL, pauseUntil);
  updatePauseStatus(pauseUntil);
  notifySettingsChanged();
}

async function resumeTracking() {
  await setSetting(STORAGE_KEYS.PAUSE_UNTIL, 0);
  updatePauseStatus(0);
  notifySettingsChanged();
}

function updatePauseStatus(pauseUntil) {
  if (!pauseUntil || Date.now() >= pauseUntil) {
    pauseStatusEl.style.display = 'none';
    pauseResumeBtn.style.display = 'none';
    pause15Btn.style.display = '';
    pause30Btn.style.display = '';
    pause60Btn.style.display = '';
    return;
  }

  const remaining = Math.ceil((pauseUntil - Date.now()) / 60000);
  pauseStatusEl.style.display = 'block';
  pauseStatusEl.textContent = `Tracking paused for ${remaining} minute${remaining !== 1 ? 's' : ''}`;
  pauseResumeBtn.style.display = '';
  pause15Btn.style.display = 'none';
  pause30Btn.style.display = 'none';
  pause60Btn.style.display = 'none';
}

function checkConnection() {
  chrome.runtime.sendMessage({ type: 'getConnectionStatus' }, (response) => {
    if (chrome.runtime.lastError) {
      setConnectionStatus(false);
      return;
    }
    setConnectionStatus(response?.connected || false);
  });
}

function setConnectionStatus(connected) {
  connectionStatus.textContent = connected ? 'Connected' : 'Disconnected';
  connectionStatus.className = `status-badge ${connected ? 'connected' : 'disconnected'}`;
}

function notifySettingsChanged() {
  chrome.runtime.sendMessage({ type: 'settingsChanged' }).catch(() => {});
}

function escapeHtml(str) {
  const div = document.createElement('div');
  div.textContent = str;
  return div.innerHTML;
}

init();

(function () {
  'use strict';

  if (window.__focusBotOverlayInitialized) return;
  window.__focusBotOverlayInitialized = true;

  const STYLES = `
    :host {
      all: initial;
      position: fixed;
      z-index: 2147483647;
      bottom: 20px;
      right: 20px;
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
      font-size: 13px;
      line-height: 1.4;
      pointer-events: auto;
    }

    .overlay {
      background: #1a1a2e;
      border-radius: 12px;
      padding: 12px 16px;
      color: #e0e0e0;
      min-width: 180px;
      max-width: 280px;
      box-shadow: 0 4px 24px rgba(0, 0, 0, 0.4), 0 0 0 1px rgba(255, 255, 255, 0.06);
      transition: opacity 0.2s, transform 0.2s;
      cursor: default;
      user-select: none;
    }

    .overlay.minimized {
      min-width: auto;
      max-width: auto;
      padding: 8px;
      border-radius: 50%;
      cursor: pointer;
    }

    .overlay.hidden {
      display: none;
    }

    .header {
      display: flex;
      align-items: center;
      gap: 8px;
      margin-bottom: 4px;
    }

    .minimized .header {
      margin-bottom: 0;
    }

    .indicator {
      width: 10px;
      height: 10px;
      border-radius: 50%;
      flex-shrink: 0;
      transition: background 0.3s;
    }

    .indicator.focused { background: #4ade80; box-shadow: 0 0 6px rgba(74, 222, 128, 0.4); }
    .indicator.unclear { background: #facc15; box-shadow: 0 0 6px rgba(250, 204, 21, 0.4); }
    .indicator.distracted { background: #f87171; box-shadow: 0 0 6px rgba(248, 113, 113, 0.4); }
    .indicator.unknown { background: #94a3b8; }
    .indicator.disconnected { background: #64748b; }

    .status-text {
      font-weight: 600;
      font-size: 13px;
      flex-grow: 1;
    }

    .status-text.focused { color: #4ade80; }
    .status-text.unclear { color: #facc15; }
    .status-text.distracted { color: #f87171; }
    .status-text.unknown { color: #94a3b8; }
    .status-text.disconnected { color: #64748b; }

    .controls {
      display: flex;
      gap: 4px;
      margin-left: auto;
    }

    .controls button {
      background: none;
      border: none;
      color: #94a3b8;
      cursor: pointer;
      font-size: 14px;
      padding: 2px 4px;
      border-radius: 4px;
      line-height: 1;
      transition: color 0.15s, background 0.15s;
    }

    .controls button:hover {
      color: #e0e0e0;
      background: rgba(255, 255, 255, 0.1);
    }

    .body {
      padding-top: 4px;
      border-top: 1px solid rgba(255, 255, 255, 0.06);
      margin-top: 4px;
    }

    .minimized .body {
      display: none;
    }

    .task-name {
      font-size: 12px;
      color: #cbd5e1;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      max-width: 240px;
    }

    .reason {
      font-size: 11px;
      color: #64748b;
      margin-top: 2px;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      max-width: 240px;
    }

    .timer {
      font-size: 11px;
      color: #64748b;
      margin-top: 4px;
      font-variant-numeric: tabular-nums;
    }
  `;

  const STATUS_MAP = {
    focused: { label: 'Focused', cssClass: 'focused' },
    unclear: { label: 'Unclear', cssClass: 'unclear' },
    distracted: { label: 'Distracted', cssClass: 'distracted' },
    unknown: { label: 'Waiting...', cssClass: 'unknown' },
    disconnected: { label: 'Disconnected', cssClass: 'disconnected' }
  };

  let isMinimized = false;
  let overlayElement = null;
  let shadowRoot = null;

  function createOverlay() {
    const host = document.createElement('div');
    host.id = 'focusbot-overlay-host';
    shadowRoot = host.attachShadow({ mode: 'closed' });

    const style = document.createElement('style');
    style.textContent = STYLES;
    shadowRoot.appendChild(style);

    overlayElement = document.createElement('div');
    overlayElement.className = 'overlay';
    overlayElement.innerHTML = buildOverlayHtml({ status: 'unknown', connected: false });
    shadowRoot.appendChild(overlayElement);

    document.documentElement.appendChild(host);
    setupEventHandlers();
  }

  function buildOverlayHtml(state) {
    const info = STATUS_MAP[state.status] || STATUS_MAP.unknown;
    const timerStr = state.sessionElapsedSeconds ? formatTimer(state.sessionElapsedSeconds) : '';

    return `
      <div class="header">
        <div class="indicator ${info.cssClass}"></div>
        <span class="status-text ${info.cssClass}">${info.label}</span>
        <div class="controls">
          <button class="btn-minimize" title="Minimize">&minus;</button>
          <button class="btn-close" title="Hide overlay">&times;</button>
        </div>
      </div>
      <div class="body">
        ${state.taskName ? `<div class="task-name">${escapeHtml(state.taskName)}</div>` : ''}
        ${state.reason ? `<div class="reason">${escapeHtml(state.reason)}</div>` : ''}
        ${timerStr ? `<div class="timer">${timerStr}</div>` : ''}
      </div>
    `;
  }

  function setupEventHandlers() {
    shadowRoot.addEventListener('click', (e) => {
      const target = e.target;

      if (target.classList.contains('btn-minimize')) {
        toggleMinimize();
        return;
      }

      if (target.classList.contains('btn-close')) {
        hideOverlay();
        return;
      }

      if (isMinimized) {
        toggleMinimize();
      }
    });
  }

  function toggleMinimize() {
    isMinimized = !isMinimized;
    if (overlayElement) {
      overlayElement.classList.toggle('minimized', isMinimized);
    }
  }

  function hideOverlay() {
    if (overlayElement) {
      overlayElement.classList.add('hidden');
    }
  }

  function showOverlay() {
    if (overlayElement) {
      overlayElement.classList.remove('hidden');
    }
  }

  function updateOverlay(state) {
    if (!overlayElement) return;
    if (overlayElement.classList.contains('hidden')) return;

    const info = STATUS_MAP[state.status] || STATUS_MAP.unknown;
    const timerStr = state.sessionElapsedSeconds ? formatTimer(state.sessionElapsedSeconds) : '';

    const indicator = shadowRoot.querySelector('.indicator');
    const statusText = shadowRoot.querySelector('.status-text');
    const body = shadowRoot.querySelector('.body');

    if (indicator) {
      indicator.className = `indicator ${info.cssClass}`;
    }

    if (statusText) {
      statusText.className = `status-text ${info.cssClass}`;
      statusText.textContent = info.label;
    }

    if (body) {
      body.innerHTML = [
        state.taskName ? `<div class="task-name">${escapeHtml(state.taskName)}</div>` : '',
        state.reason ? `<div class="reason">${escapeHtml(state.reason)}</div>` : '',
        timerStr ? `<div class="timer">${timerStr}</div>` : ''
      ].filter(Boolean).join('');
    }
  }

  function formatTimer(totalSeconds) {
    const h = Math.floor(totalSeconds / 3600);
    const m = Math.floor((totalSeconds % 3600) / 60);
    const s = totalSeconds % 60;
    return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
  }

  function escapeHtml(str) {
    const div = document.createElement('div');
    div.textContent = str;
    return div.innerHTML;
  }

  chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
    if (message.type === 'focusStateUpdate' && message.data) {
      if (!overlayElement) {
        createOverlay();
      }
      updateOverlay(message.data);
    }

    if (message.type === 'toggleOverlay') {
      if (overlayElement) {
        if (overlayElement.classList.contains('hidden')) {
          showOverlay();
        } else {
          hideOverlay();
        }
      }
    }

    return false;
  });

  chrome.runtime.sendMessage({ type: 'getFocusState' }, (response) => {
    if (chrome.runtime.lastError) return;
    if (response) {
      createOverlay();
      updateOverlay(response);
    }
  });
})();

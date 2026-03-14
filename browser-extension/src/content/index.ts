const SESSION_STORAGE_KEY = "focusbot-distraction-tooltip-session";

interface DistractionAlertMessage {
  type: "FOCUSBOT_DISTRACTION_ALERT";
  show: boolean;
  sessionId?: string;
  taskText?: string;
  domain?: string;
  reason?: string;
}

const TOOLTIP_ID = "focusbot-distraction-tooltip";
const TOOLTIP_TEXT_ID = "focusbot-distraction-tooltip-text";
const AUTO_HIDE_MS = 5000;

let autoHideTimer: ReturnType<typeof setTimeout> | null = null;

const clearAutoHideTimer = (): void => {
  if (autoHideTimer !== null) {
    clearTimeout(autoHideTimer);
    autoHideTimer = null;
  }
};

const ensureTooltip = (): { container: HTMLElement; textEl: HTMLElement } => {
  const existing = document.getElementById(TOOLTIP_ID);
  let textEl = existing ? document.getElementById(TOOLTIP_TEXT_ID) : null;
  if (existing && textEl) {
    return { container: existing, textEl };
  }

  const container = existing ?? document.createElement("div");
  if (!existing) {
    container.id = TOOLTIP_ID;
    container.className = "focusbot-distraction-tooltip hidden";
    document.documentElement.appendChild(container);
  }
  container.innerHTML = "";

  const span = document.createElement("span");
  span.id = TOOLTIP_TEXT_ID;
  span.className = "focusbot-distraction-tooltip-text";
  textEl = span;

  const dismissBtn = document.createElement("button");
  dismissBtn.type = "button";
  dismissBtn.className = "focusbot-distraction-tooltip-dismiss";
  dismissBtn.setAttribute("aria-label", "Dismiss");
  dismissBtn.textContent = "\u00D7";

  container.appendChild(span);
  container.appendChild(dismissBtn);

  dismissBtn.addEventListener("click", () => {
    clearAutoHideTimer();
    hideTooltip();
  });

  return { container, textEl };
};

const showTooltip = (message: string): void => {
  clearAutoHideTimer();
  const { container, textEl } = ensureTooltip();
  textEl.textContent = message;
  container.classList.remove("hidden");
  autoHideTimer = setTimeout(() => {
    autoHideTimer = null;
    hideTooltip();
  }, AUTO_HIDE_MS);
};

const hideTooltip = (): void => {
  clearAutoHideTimer();
  const tooltip = document.getElementById(TOOLTIP_ID);
  if (!tooltip) {
    return;
  }
  tooltip.classList.add("hidden");
};

chrome.runtime.onMessage.addListener((message: DistractionAlertMessage) => {
  if (!message || message.type !== "FOCUSBOT_DISTRACTION_ALERT") {
    return;
  }

  if (!message.show) {
    try {
      sessionStorage.removeItem(SESSION_STORAGE_KEY);
    } catch {
      /* ignore */
    }
    hideTooltip();
    return;
  }

  const sessionId = message.sessionId ?? "";
  try {
    if (sessionStorage.getItem(SESSION_STORAGE_KEY) === sessionId) {
      return;
    }
    sessionStorage.setItem(SESSION_STORAGE_KEY, sessionId);
  } catch {
    /* show anyway if storage fails */
  }

  const domain = message.domain ?? "This site";
  let because = message.reason?.trim() ?? "";
  const redundantPrefix = /^\s*this (?:page|site) is not aligned (?:with (?:the )?current task )?because\s+/i;
  if (redundantPrefix.test(because)) {
    because = because.replace(redundantPrefix, "").trim();
  }
  if (!because) {
    because = "it doesn't match your current task.";
  }
  const reasonSuffix = because.endsWith(".") ? because : `${because}.`;
  showTooltip(`${domain} doesn't seem to be aligned with the current task because ${reasonSuffix}`);
});

chrome.runtime.sendMessage({ type: "FOCUSBOT_CONTENT_READY" });

interface DistractionAlertMessage {
  type: "FOCUSBOT_DISTRACTION_ALERT";
  show: boolean;
  taskText?: string;
  domain?: string;
}

const TOOLTIP_ID = "focusbot-distraction-tooltip";

const ensureTooltip = (): HTMLElement => {
  const existing = document.getElementById(TOOLTIP_ID);
  if (existing) {
    return existing;
  }

  const tooltip = document.createElement("div");
  tooltip.id = TOOLTIP_ID;
  tooltip.className = "focusbot-distraction-tooltip hidden";
  document.documentElement.appendChild(tooltip);
  return tooltip;
};

const showTooltip = (message: string): void => {
  const tooltip = ensureTooltip();
  tooltip.textContent = message;
  tooltip.classList.remove("hidden");
};

const hideTooltip = (): void => {
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
    hideTooltip();
    return;
  }

  const domainPart = message.domain ? ` (${message.domain})` : "";
  const taskPart = message.taskText ? `Task: ${message.taskText}` : "Current task";
  showTooltip(`Distracting website detected${domainPart}. ${taskPart}.`);
});

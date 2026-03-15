import { defineManifest } from "@crxjs/vite-plugin";

export default defineManifest({
  manifest_version: 3,
  name: "Focus Bot",
  version: "0.1.0",
  description:
    "Single-task assistant: see if your browsing matches your task, get session summaries and daily analytics.",
  icons: {
    16: "icons/icon-default-16.png",
    32: "icons/icon-default-32.png",
    48: "icons/icon-default-48.png",
    96: "icons/icon-default-96.png"
  },
  permissions: ["storage", "tabs", "sidePanel", "alarms"],
  host_permissions: ["https://api.openai.com/*", "<all_urls>"],
  action: {
    default_title: "Focus Bot",
    default_popup: "src/popup/index.html",
    default_icons: {
      16: "icons/icon-default-16.png",
      32: "icons/icon-default-32.png",
      48: "icons/icon-default-48.png",
      96: "icons/icon-default-96.png"
    }
  },
  side_panel: {
    default_path: "src/sidepanel/index.html"
  },
  content_scripts: [
    {
      matches: ["<all_urls>"],
      js: ["src/content/index.ts"],
      css: ["focusbot-tooltip.css"],
      run_at: "document_idle"
    }
  ],
  options_page: "src/options/index.html",
  background: {
    service_worker: "src/background/index.ts",
    type: "module"
  },
  web_accessible_resources: [
    {
      resources: [
        "icons/icon-default-16.png",
        "icons/icon-default-32.png",
        "icons/icon-default-48.png",
        "icons/icon-default-96.png",
        "icons/icon-aligned.svg",
        "icons/icon-distracting.svg",
        "icons/icon-analyzing.svg",
        "icons/icon-error.svg"
      ],
      matches: ["<all_urls>"]
    }
  ]
});

import { defineManifest } from "@crxjs/vite-plugin";

export default defineManifest({
  manifest_version: 3,
  name: "FocusBot",
  version: "0.1.0",
  description:
    "Single-task assistant that classifies whether your browsing matches your stated task, summarizes sessions, and tracks daily analytics. Supports any kind of work or break.",
  permissions: ["storage", "tabs", "sidePanel"],
  host_permissions: ["https://api.openai.com/*", "<all_urls>"],
  action: {
    default_title: "FocusBot",
    default_popup: "src/popup/index.html",
    default_icons: {
      16: "icons/icon-default.svg",
      32: "icons/icon-default.svg",
      48: "icons/icon-default.svg",
      96: "icons/icon-default.svg"
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
        "icons/icon-default.svg",
        "icons/icon-aligned.svg",
        "icons/icon-distracting.svg",
        "icons/icon-analyzing.svg",
        "icons/icon-error.svg"
      ],
      matches: ["<all_urls>"]
    }
  ]
});

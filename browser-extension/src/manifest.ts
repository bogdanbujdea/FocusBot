import { defineManifest } from "@crxjs/vite-plugin";

export default defineManifest({
  manifest_version: 3,
  name: "FocusBot Deep Work",
  version: "0.1.0",
  description:
    "Single-task deep work assistant that classifies browsing alignment, summarizes sessions, and tracks daily analytics.",
  permissions: ["storage", "tabs", "sidePanel"],
  host_permissions: ["https://api.openai.com/*", "<all_urls>"],
  action: {
    default_title: "FocusBot Deep Work",
    default_popup: "src/popup/index.html"
  },
  side_panel: {
    default_path: "src/sidepanel/index.html"
  },
  options_page: "src/options/index.html",
  background: {
    service_worker: "src/background/index.ts",
    type: "module"
  }
});

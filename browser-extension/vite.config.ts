import { crx } from "@crxjs/vite-plugin";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";
import manifest from "./src/manifest";

export default defineConfig({
  plugins: [react(), crx({ manifest })],
  build: {
    rollupOptions: {
      onwarn(warning, warn) {
        const isSignalRPureAnnotationWarning =
          typeof warning.message === "string" &&
          warning.message.includes("contains an annotation that Rollup cannot interpret") &&
          typeof warning.id === "string" &&
          warning.id.includes("@microsoft/signalr/dist/esm/Utils.js");

        if (isSignalRPureAnnotationWarning) {
          return;
        }

        warn(warning);
      },
      input: {
        authCallback: "auth-callback.html"
      }
    }
  },
  test: {
    environment: "node",
    include: ["tests/**/*.test.ts"]
  }
});

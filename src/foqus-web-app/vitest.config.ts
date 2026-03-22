import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  test: {
    environment: "jsdom",
    globals: false,
    setupFiles: ["./src/test/setup.ts"],
    env: {
      VITE_SUPABASE_URL: "http://127.0.0.1",
      VITE_SUPABASE_ANON_KEY: "test-anon-key-for-vitest",
    },
  },
});

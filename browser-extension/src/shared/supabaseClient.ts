import { createClient } from "@supabase/supabase-js";

// For now we use the same Supabase project as the backend.
// These values should be kept in sync with the WebAPI configuration.
const SUPABASE_URL = "https://mokjfxtnqmudypnukqsv.supabase.co";
const SUPABASE_ANON_KEY = "sb_publishable_U9dKqMzxtpms_EGvvUybCg_IKGoPc3t";

export const supabase = createClient(SUPABASE_URL, SUPABASE_ANON_KEY, {
  auth: {
    persistSession: true,
    storageKey: "focusbot.supabase",
    storage: {
      getItem: (key: string): string | null => {
        // Supabase auth storage is only used from the options/settings UI,
        // which runs in a document context where localStorage is available.
        try {
          return window.localStorage.getItem(key);
        } catch {
          return null;
        }
      },
      setItem: (key: string, value: string): void => {
        try {
          window.localStorage.setItem(key, value);
        } catch {
          // Ignore storage errors; the user will simply need to sign in again.
        }
      },
      removeItem: (key: string): void => {
        try {
          window.localStorage.removeItem(key);
        } catch {
          // Ignore storage errors.
        }
      }
    }
  }
});


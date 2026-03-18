import { APP_KEYS } from "./utils";
import type { Settings } from "./types";
import { loadSettings, patchSettings } from "./storage";

const STORAGE_KEY_SUPABASE_ACCESS = "focusbot.supabaseAccessToken";
const STORAGE_KEY_SUPABASE_EMAIL = "focusbot.supabaseEmail";

export interface FocusbotAuthSession {
  accessToken: string;
  email: string;
}

export const loadFocusbotAuthSession = async (): Promise<FocusbotAuthSession | null> => {
  const record = await chrome.storage.local.get([STORAGE_KEY_SUPABASE_ACCESS, STORAGE_KEY_SUPABASE_EMAIL]);
  const accessToken = record[STORAGE_KEY_SUPABASE_ACCESS] as string | undefined;
  const email = record[STORAGE_KEY_SUPABASE_EMAIL] as string | undefined;

  if (!accessToken || !email) {
    return null;
  }

  return { accessToken, email };
};

export const saveFocusbotAuthSession = async (session: FocusbotAuthSession): Promise<void> => {
  await chrome.storage.local.set({
    [STORAGE_KEY_SUPABASE_ACCESS]: session.accessToken,
    [STORAGE_KEY_SUPABASE_EMAIL]: session.email
  });

  const settings = await loadSettings();
  const next: Settings = {
    ...settings,
    authMode: "foqus-account",
    focusbotEmail: session.email,
    onboardingCompleted: true
  };
  await patchSettings(next);
};

export const clearFocusbotAuthSession = async (): Promise<void> => {
  await chrome.storage.local.remove([STORAGE_KEY_SUPABASE_ACCESS, STORAGE_KEY_SUPABASE_EMAIL, APP_KEYS.settings]);
};


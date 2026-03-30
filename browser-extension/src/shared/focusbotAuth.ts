import { APP_KEYS } from "./utils";
import { patchSettings } from "./storage";

const STORAGE_KEY_SUPABASE_ACCESS = "focusbot.supabaseAccessToken";
const STORAGE_KEY_SUPABASE_REFRESH = "focusbot.supabaseRefreshToken";
const STORAGE_KEY_SUPABASE_EMAIL = "focusbot.supabaseEmail";

export interface FocusbotAuthSession {
  accessToken: string;
  refreshToken: string;
  email: string;
}

export const loadFocusbotAuthSession = async (): Promise<FocusbotAuthSession | null> => {
  const record = await chrome.storage.local.get([
    STORAGE_KEY_SUPABASE_ACCESS,
    STORAGE_KEY_SUPABASE_REFRESH,
    STORAGE_KEY_SUPABASE_EMAIL
  ]);
  const accessToken = record[STORAGE_KEY_SUPABASE_ACCESS] as string | undefined;
  const refreshToken = record[STORAGE_KEY_SUPABASE_REFRESH] as string | undefined;
  const email = record[STORAGE_KEY_SUPABASE_EMAIL] as string | undefined;

  if (!accessToken || !email) {
    return null;
  }

  return { accessToken, refreshToken: refreshToken ?? "", email };
};

export const saveFocusbotAuthSession = async (session: FocusbotAuthSession): Promise<void> => {
  await chrome.storage.local.set({
    [STORAGE_KEY_SUPABASE_ACCESS]: session.accessToken,
    [STORAGE_KEY_SUPABASE_REFRESH]: session.refreshToken,
    [STORAGE_KEY_SUPABASE_EMAIL]: session.email
  });

  await patchSettings({
    focusbotEmail: session.email,
    onboardingCompleted: true
  });
};

export const clearFocusbotAuthSession = async (): Promise<void> => {
  await chrome.storage.local.remove([
    STORAGE_KEY_SUPABASE_ACCESS,
    STORAGE_KEY_SUPABASE_REFRESH,
    STORAGE_KEY_SUPABASE_EMAIL,
    APP_KEYS.clientId,
    APP_KEYS.clientFingerprint,
    APP_KEYS.deviceId,
    APP_KEYS.deviceFingerprint
  ]);
};

/**
 * Attempts to refresh the Supabase access token using the stored refresh token.
 * Updates storage on success. Returns true if the token was refreshed.
 */
export const refreshFocusbotAuthToken = async (): Promise<boolean> => {
  const session = await loadFocusbotAuthSession();
  if (!session?.refreshToken) return false;

  try {
    const supabaseUrl = "https://mokjfxtnqmudypnukqsv.supabase.co";
    const supabaseAnonKey = "sb_publishable_U9dKqMzxtpms_EGvvUybCg_IKGoPc3t";
    const response = await fetch(`${supabaseUrl}/auth/v1/token?grant_type=refresh_token`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        apikey: supabaseAnonKey
      },
      body: JSON.stringify({ refresh_token: session.refreshToken })
    });

    if (!response.ok) {
      console.warn("[Foqus] Supabase token refresh failed:", response.status);
      return false;
    }

    const data = (await response.json()) as {
      access_token?: string;
      refresh_token?: string;
    };

    if (!data.access_token) return false;

    await chrome.storage.local.set({
      [STORAGE_KEY_SUPABASE_ACCESS]: data.access_token,
      [STORAGE_KEY_SUPABASE_REFRESH]: data.refresh_token ?? session.refreshToken
    });

    return true;
  } catch (error) {
    console.warn("[Foqus] Supabase token refresh error:", error);
    return false;
  }
};


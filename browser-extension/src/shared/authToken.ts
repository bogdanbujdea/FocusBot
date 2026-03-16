import type { AuthTokenPayload } from "./integrationTypes";
import { MESSAGE_TYPES } from "./integrationTypes";
import { sendMessage } from "./integration";

const STORAGE_KEY_ACCESS = "focusbot.accessToken";
const STORAGE_KEY_REFRESH = "focusbot.refreshToken";

export const storeTokens = async (payload: AuthTokenPayload): Promise<void> => {
  await chrome.storage.local.set({
    [STORAGE_KEY_ACCESS]: payload.accessToken,
    [STORAGE_KEY_REFRESH]: payload.refreshToken
  });
};

export const getAccessToken = async (): Promise<string | null> => {
  const result = await chrome.storage.local.get(STORAGE_KEY_ACCESS);
  return (result[STORAGE_KEY_ACCESS] as string) ?? null;
};

export const getRefreshToken = async (): Promise<string | null> => {
  const result = await chrome.storage.local.get(STORAGE_KEY_REFRESH);
  return (result[STORAGE_KEY_REFRESH] as string) ?? null;
};

export const clearTokens = async (): Promise<void> => {
  await chrome.storage.local.remove([STORAGE_KEY_ACCESS, STORAGE_KEY_REFRESH]);
};

export const refreshAccessToken = async (): Promise<boolean> => {
  const refreshToken = await getRefreshToken();
  if (!refreshToken) return false;

  try {
    const response = await fetch("https://api.focusbot.app/auth/refresh", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ refreshToken })
    });

    if (!response.ok) {
      console.warn("[FocusBot] Token refresh failed:", response.status);
      return false;
    }

    const data = (await response.json()) as AuthTokenPayload;
    await storeTokens(data);
    return true;
  } catch (error) {
    console.warn("[FocusBot] Token refresh error:", error);
    return false;
  }
};

export const handleAuthTokenMessage = async (payload: AuthTokenPayload): Promise<void> => {
  await storeTokens(payload);
};

export const sendAuthTokenToDesktop = async (): Promise<void> => {
  const accessToken = await getAccessToken();
  const refreshToken = await getRefreshToken();
  if (!accessToken || !refreshToken) return;
  const payload: AuthTokenPayload = { accessToken, refreshToken };
  sendMessage(MESSAGE_TYPES.AUTH_TOKEN, payload);
};

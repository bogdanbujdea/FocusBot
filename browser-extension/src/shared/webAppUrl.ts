const trimTrailingSlashes = (url: string): string => url.replace(/\/+$/, "");

export const getWebAppBaseUrl = (): string => {
  const configured = (import.meta as unknown as { env?: { VITE_FOQUS_WEB_APP_URL?: string } }).env
    ?.VITE_FOQUS_WEB_APP_URL;
  if (configured?.trim()) return trimTrailingSlashes(configured.trim());

  const isDev = Boolean((import.meta as unknown as { env?: { DEV?: boolean } }).env?.DEV);
  return isDev ? "http://localhost:5174" : "https://app.foqus.me";
};

export const getWebAppAnalyticsUrl = (): string => `${getWebAppBaseUrl()}/analytics`;

export const getWebAppBillingUrl = (): string => `${getWebAppBaseUrl()}/billing`;

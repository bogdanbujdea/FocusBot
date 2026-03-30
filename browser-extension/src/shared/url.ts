const NON_TRACKABLE_SCHEMES = ["chrome://", "edge://", "about:", "chrome-extension://"];

export const isTrackableUrl = (url: string): boolean => {
  if (!url) {
    return false;
  }

  return !NON_TRACKABLE_SCHEMES.some((scheme) => url.startsWith(scheme));
};

export const getDomain = (url: string): string => {
  try {
    const parsed = new URL(url);
    return parsed.hostname.toLowerCase();
  } catch {
    return "unknown-domain";
  }
};

export const normalizeDomain = (domain: string): string =>
  domain.trim().toLowerCase().replace(/^\*\./, "").replace(/\.$/, "");

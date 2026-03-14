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

export const parseExcludedDomains = (input: string): string[] =>
  input
    .split(/[\n,]/g)
    .map(normalizeDomain)
    .filter((domain) => domain.length > 0);

export const matchesExcludedDomain = (domain: string, excludedDomains: string[]): boolean => {
  const normalizedDomain = normalizeDomain(domain);

  return excludedDomains.some((rawCandidate) => {
    const candidate = normalizeDomain(rawCandidate);
    return normalizedDomain === candidate || normalizedDomain.endsWith(`.${candidate}`);
  });
};

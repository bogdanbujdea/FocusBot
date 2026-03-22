function parseHashParams(hash: string): Record<string, string> {
  const params: Record<string, string> = {};
  const slice = hash.startsWith("#") ? hash.slice(1) : hash;
  for (const part of slice.split("&")) {
    const [k, ...v] = part.split("=");
    if (k) {
      params[decodeURIComponent(k)] = decodeURIComponent(v.join("="));
    }
  }
  return params;
}

function decodeJwtPayload(jwt: string): { email?: string; user_metadata?: { email?: string } } | null {
  try {
    const base64 = jwt.split(".")[1]?.replace(/-/g, "+").replace(/_/g, "/");
    if (!base64) return null;
    const json = decodeURIComponent(
      atob(base64)
        .split("")
        .map((c) => "%" + c.charCodeAt(0).toString(16).padStart(2, "0"))
        .join("")
    );
    return JSON.parse(json) as { email?: string; user_metadata?: { email?: string } };
  } catch {
    return null;
  }
}

const msgEl = document.getElementById("msg");
const subEl = document.getElementById("sub");

const setMsg = (title: string, sub: string): void => {
  if (msgEl) msgEl.textContent = title;
  if (subEl) subEl.textContent = sub;
};

const params = parseHashParams(window.location.hash);
const accessToken = params["access_token"];
const refreshToken = params["refresh_token"] ?? "";

if (!accessToken) {
  setMsg("Sign-in failed.", "No access token found in the link. Please try again.");
} else {
  const payload = decodeJwtPayload(accessToken);
  const email = payload?.email || payload?.user_metadata?.email || "";

  if (!email) {
    setMsg("Sign-in failed.", "Could not read account email from token. Please try again.");
  } else {
    window.postMessage(
      { type: "FOCUSBOT_AUTH_CALLBACK", accessToken, refreshToken, email },
      "*"
    );

    void (async () => {
      try {
        await chrome.runtime.sendMessage({
          type: "FOCUSBOT_AUTH_FROM_EXTENSION_CALLBACK",
          accessToken,
          refreshToken,
          email
        });
        setMsg(`Signed in as ${email}`, "You can close this tab.");
      } catch {
        setMsg("Sign-in failed.", "Could not reach the extension. Reload Foqus and try the magic link again.");
      }
      setTimeout(() => {
        try {
          window.close();
        } catch {
          /* ignore */
        }
      }, 2000);
    })();
  }
}

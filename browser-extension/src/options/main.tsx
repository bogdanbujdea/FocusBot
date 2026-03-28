import { useEffect, useMemo, useState } from "react";
import { createRoot } from "react-dom/client";
import { sendRuntimeRequest } from "../shared/runtime";
import type { PlanType, Settings } from "../shared/types";
import { supabase } from "../shared/supabaseClient";
import { loadFocusbotAuthSession } from "../shared/focusbotAuth";
import { parseExcludedDomains } from "../shared/url";
import { getWebAppAnalyticsUrl } from "../shared/webAppUrl";
import "../ui/styles.css";
import "./settings.css";

const PLAN_CHECKOUT_URLS: Record<string, string> = {
  "cloud-byok": "https://app.foqus.me/checkout/cloud-byok",
  "cloud-managed": "https://app.foqus.me/checkout/cloud-managed"
};

const PLAN_LABELS: Record<PlanType, string> = {
  "free-byok": "Free",
  "cloud-byok": "Cloud BYOK",
  "cloud-managed": "Cloud Managed"
};

const SettingsPage = (): JSX.Element => {
  const [settings, setSettings] = useState<Settings>({
    plan: "free-byok",
    openAiApiKey: "",
    classifierModel: "gpt-4o-mini",
    onboardingCompleted: false,
    excludedDomains: [],
    desktopAppIntegration: false
  });
  const [excludedDomainsInput, setExcludedDomainsInput] = useState("");
  const [status, setStatus] = useState("");
  const [signedInEmail, setSignedInEmail] = useState<string | null>(null);
  const [focusbotEmailInput, setFocusbotEmailInput] = useState("");
  const [focusbotStatus, setFocusbotStatus] = useState("");

  // Load settings and current auth session on mount.
  useEffect(() => {
    void (async () => {
      const [response, session] = await Promise.all([
        sendRuntimeRequest<{ settings: Settings }>({ type: "GET_STATE" }),
        loadFocusbotAuthSession()
      ]);
      if (response.ok && response.data) {
        setSettings(response.data.settings);
        setExcludedDomainsInput(response.data.settings.excludedDomains.join("\n"));
      }
      if (session) {
        setSignedInEmail(session.email);
      }
    })();
  }, []);

  // Reactively pick up tokens stored by the content script after
  // the magic-link callback page posts them via window.postMessage.
  useEffect(() => {
    const onChanged = (changes: { [key: string]: chrome.storage.StorageChange }) => {
      if (changes["focusbot.supabaseAccessToken"] || changes["focusbot.supabaseEmail"]) {
        void (async () => {
          const session = await loadFocusbotAuthSession();
          if (session) {
            setSignedInEmail(session.email);
            setFocusbotStatus(`Signed in as ${session.email}`);
          }
        })();
      }
    };
    chrome.storage.local.onChanged.addListener(onChanged);
    return () => chrome.storage.local.onChanged.removeListener(onChanged);
  }, []);

  const excludedPreview = useMemo(() => parseExcludedDomains(excludedDomainsInput), [excludedDomainsInput]);
  const isAuthenticated = signedInEmail !== null;
  const needsApiKey = settings.plan === "free-byok" || settings.plan === "cloud-byok";

  const save = async (): Promise<void> => {
    setStatus("Saving...");
    const response = await sendRuntimeRequest<Settings>({
      type: "UPDATE_SETTINGS",
      payload: {
        ...settings,
        excludedDomains: excludedPreview,
        onboardingCompleted: true
      }
    });

    if (!response.ok) {
      setStatus(response.error ?? "Unable to save settings.");
      return;
    }

    setStatus("Saved.");
  };

  const handleSignOut = async (): Promise<void> => {
    await sendRuntimeRequest({ type: "SIGN_OUT" });
    await supabase.auth.signOut();
    setSignedInEmail(null);
    setFocusbotStatus("Signed out.");
  };

  const handleSendMagicLink = async (): Promise<void> => {
    if (!focusbotEmailInput.trim()) {
      setFocusbotStatus("Enter an email address first.");
      return;
    }
    setFocusbotStatus("Sending magic link...");
    const { error } = await supabase.auth.signInWithOtp({
      email: focusbotEmailInput.trim(),
      options: {
        shouldCreateUser: true,
        emailRedirectTo: chrome.runtime.getURL("auth-callback.html")
      }
    });
    if (error) {
      setFocusbotStatus(error.message);
      return;
    }
    setFocusbotStatus("Magic link sent. Open the link from this device to finish sign-in.");
  };

  return (
    <main className="app-shell settings-page">
      <header>
        <h1>Foqus Settings</h1>
        <p className="muted">Configure your account, plan, and classifier behavior.</p>
      </header>

      {/* Account section — always shown. Shows sign-in wall when not authenticated. */}
      <section className="card">
        <h2>Account</h2>
        {!isAuthenticated ? (
          <>
            <p className="muted">
              A Foqus account is required to use the extension. Sign in to start tracking focus sessions.
            </p>
            <div className="settings-form">
              <div className="settings-field">
                <label className="label" htmlFor="focusbot-email">Email</label>
                <input
                  id="focusbot-email"
                  type="email"
                  value={focusbotEmailInput}
                  onChange={(event) => setFocusbotEmailInput(event.target.value)}
                  placeholder="you@example.com"
                />
              </div>
              <div className="settings-inline-actions">
                <button type="button" onClick={() => void handleSendMagicLink()}>
                  Send magic link
                </button>
              </div>
              {focusbotStatus ? <p className="muted">{focusbotStatus}</p> : null}
            </div>
          </>
        ) : (
          <div className="settings-form">
            <p className="muted">
              Signed in as <strong>{signedInEmail}</strong>
              {" · "}
              <span className="pill">{PLAN_LABELS[settings.plan]}</span>
            </p>
            <div className="settings-inline-actions">
              <button type="button" onClick={() => void handleSignOut()}>Sign out</button>
            </div>
            {focusbotStatus ? <p className="muted">{focusbotStatus}</p> : null}
          </div>
        )}
      </section>

      {/* Plan selection — only shown when authenticated */}
      {isAuthenticated ? (
        <section className="card">
          <h2>Plan</h2>
          <p className="muted">Your current plan determines how classifications are run and whether sessions sync to the cloud.</p>
          <div className="settings-auth-cards">
            {(["free-byok", "cloud-byok", "cloud-managed"] as PlanType[]).map((plan) => {
              const isCurrentPlan = settings.plan === plan;
              const isPaid = plan !== "free-byok";
              return (
                <div key={plan} className="settings-radio-card" data-selected={isCurrentPlan}>
                  <span className="settings-radio-card-body">
                    <span className="settings-radio-card-title">
                      {plan === "free-byok" && "Free — Bring your own key"}
                      {plan === "cloud-byok" && "Cloud BYOK — $2.99/mo"}
                      {plan === "cloud-managed" && "Cloud Managed — $4.99/mo"}
                    </span>
                    <span className="settings-radio-card-desc">
                      {plan === "free-byok" && "Use your own OpenAI key. Local analytics only. No cloud sync."}
                      {plan === "cloud-byok" && "Your API key, cloud session sync and full analytics on foqus.me."}
                      {plan === "cloud-managed" && "Platform API key, cloud sync, full analytics. No key needed."}
                    </span>
                  </span>
                  {isCurrentPlan ? (
                    <span className="pill">Current plan</span>
                  ) : isPaid ? (
                    <a
                      href={PLAN_CHECKOUT_URLS[plan]}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="button-link"
                    >
                      Select →
                    </a>
                  ) : null}
                </div>
              );
            })}
          </div>

          {/* API key input — only for plans that require a user-provided key */}
          {needsApiKey ? (
            <div className="settings-form" style={{ marginTop: "16px" }}>
              <div className="settings-field">
                <label className="label" htmlFor="api-key">OpenAI API Key</label>
                <input
                  id="api-key"
                  type="password"
                  value={settings.openAiApiKey}
                  onChange={(event) => setSettings((current) => ({ ...current, openAiApiKey: event.target.value }))}
                  placeholder="sk-..."
                />
              </div>
              <div className="settings-field">
                <label className="label" htmlFor="model">Classifier model</label>
                <input
                  id="model"
                  type="text"
                  value={settings.classifierModel}
                  onChange={(event) => setSettings((current) => ({ ...current, classifierModel: event.target.value }))}
                />
              </div>
              <div className="settings-help">
                <p className="muted">
                  Task text and page URL/title are sent to OpenAI for classification. Page body content is not sent.
                </p>
              </div>
            </div>
          ) : null}

          {settings.plan !== "free-byok" ? (
            <p className="muted" style={{ marginTop: "12px" }}>
              <a href={getWebAppAnalyticsUrl()} target="_blank" rel="noopener noreferrer">
                View full analytics →
              </a>
            </p>
          ) : null}
        </section>
      ) : null}

      {/* Classifier behavior — only shown when authenticated */}
      {isAuthenticated ? (
        <section className="card">
          <h2>Classifier behavior</h2>
          <div className="settings-field" style={{ marginBottom: "16px" }}>
            <label className="settings-checkbox-row">
              <input
                type="checkbox"
                checked={settings.desktopAppIntegration === true}
                onChange={(event) =>
                  setSettings((current) => ({ ...current, desktopAppIntegration: event.target.checked }))
                }
              />
              <span>Connect to Foqus for Windows on this computer</span>
            </label>
            <p className="muted" style={{ marginTop: "8px", marginBottom: 0 }}>
              Enables a local WebSocket to sync tasks with the desktop app. Leave off if you use the extension only in
              the browser to avoid connection errors when the desktop app is not running.
            </p>
          </div>
          <label className="label" htmlFor="excluded-domains">
            Excluded domains (comma or newline separated)
          </label>
          <textarea
            id="excluded-domains"
            rows={7}
            value={excludedDomainsInput}
            onChange={(event) => setExcludedDomainsInput(event.target.value)}
            placeholder={"localhost\ninternal.company.com"}
          />
          <p className="muted">Excluded domains bypass classification and are counted as aligned visits.</p>
          <div className="actions-row">
            <button onClick={() => void save()}>Save settings</button>
            <span className="pill">{excludedPreview.length} excluded domains</span>
          </div>
          {status ? <p className="muted">{status}</p> : null}
        </section>
      ) : null}
    </main>
  );
};

const container = document.getElementById("root");
if (!container) {
  throw new Error("Root container not found.");
}

createRoot(container).render(<SettingsPage />);

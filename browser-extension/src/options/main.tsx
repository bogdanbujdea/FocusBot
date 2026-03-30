import { useEffect, useState } from "react";
import { createRoot } from "react-dom/client";
import { sendRuntimeRequest } from "../shared/runtime";
import type { PlanType, Settings } from "../shared/types";
import { supabase } from "../shared/supabaseClient";
import { loadFocusbotAuthSession } from "../shared/focusbotAuth";
import { getWebAppBillingUrl } from "../shared/webAppUrl";
import { getPlanLabelFromServerPlanType, isByokKeyMissing, isExpiredTrial } from "../shared/subscription";
import { BYOKInfoDialog } from "../ui/BYOKInfoDialog";
import "../ui/styles.css";
import "./settings.css";

const webAppBillingUrl = getWebAppBillingUrl();

const PLAN_LABELS: Record<PlanType, string> = {
  trial: "Trial (24h)",
  "cloud-byok": "Cloud BYOK",
  "cloud-managed": "Cloud Managed"
};

const SettingsPage = (): JSX.Element => {
  const [settings, setSettings] = useState<Settings>({
    plan: "trial",
    openAiApiKey: "",
    classifierModel: "gpt-4o-mini",
    onboardingCompleted: false
  });
  const [status, setStatus] = useState("");
  const [signedInEmail, setSignedInEmail] = useState<string | null>(null);
  const [focusbotEmailInput, setFocusbotEmailInput] = useState("");
  const [focusbotStatus, setFocusbotStatus] = useState("");
  const [showApiKey, setShowApiKey] = useState(false);
  const [showByokInfoDialog, setShowByokInfoDialog] = useState(false);

  // Load settings and current auth session on mount.
  useEffect(() => {
    void (async () => {
      const [response, session] = await Promise.all([
        sendRuntimeRequest<{ settings: Settings }>({ type: "GET_STATE" }),
        loadFocusbotAuthSession()
      ]);
      if (response.ok && response.data) {
        setSettings(response.data.settings);
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

  const isAuthenticated = signedInEmail !== null;
  const isByokMissing = isByokKeyMissing(settings, isAuthenticated);
  const trialExpired = isExpiredTrial(
    settings.subscriptionStatus,
    settings.serverPlanType,
    settings.trialEndsAt
  );
  const subscriptionEndDate = settings.serverPlanType === 0
    ? settings.trialEndsAt
    : settings.currentPeriodEndsAt;
  const subscriptionEndDateLabel = subscriptionEndDate
    ? new Date(subscriptionEndDate).toLocaleString()
    : "Not available";

  const refreshPlan = async (): Promise<void> => {
    setStatus("Refreshing plan...");
    const response = await sendRuntimeRequest({ type: "REFRESH_PLAN" });
    if (!response.ok) {
      setStatus(response.error ?? "Unable to refresh plan.");
      return;
    }
    const latest = await sendRuntimeRequest<{ settings: Settings }>({ type: "GET_STATE" });
    if (latest.ok && latest.data) {
      setSettings(latest.data.settings);
    }
    setStatus("Plan refreshed.");
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

  const updateSettings = async (payload: Partial<Settings>, successMessage: string): Promise<void> => {
    setStatus("Saving settings...");
    const response = await sendRuntimeRequest({ type: "UPDATE_SETTINGS", payload });
    if (!response.ok) {
      setStatus(response.error ?? "Unable to save settings.");
      return;
    }
    const latest = await sendRuntimeRequest<{ settings: Settings }>({ type: "GET_STATE" });
    if (latest.ok && latest.data) {
      setSettings(latest.data.settings);
    }
    setStatus(successMessage);
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
            <div className="settings-subscription-summary">
              <p className="muted">
                <strong>Current plan:</strong>{" "}
                {trialExpired ? "No active plan" : getPlanLabelFromServerPlanType(settings.serverPlanType)}
              </p>
              <p className="muted">
                <strong>End date:</strong> {subscriptionEndDateLabel}
              </p>
              <div className="settings-inline-actions">
                <a
                  href={webAppBillingUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="button-link"
                >
                  {trialExpired ? "View plans" : "Manage subscription"}
                </a>
                <button type="button" onClick={() => void refreshPlan()}>
                  Refresh
                </button>
              </div>
            </div>
            <div className="settings-inline-actions">
              <button type="button" onClick={() => void handleSignOut()}>Sign out</button>
            </div>
            {focusbotStatus ? <p className="muted">{focusbotStatus}</p> : null}
          </div>
        )}
      </section>

      {status ? <p className="muted">{status}</p> : null}

      {isAuthenticated ? (
        <section className={`card ${isByokMissing ? "settings-byok-required" : ""}`}>
          <h2>OpenAI API Key</h2>
          {isByokMissing ? (
            <div className="byok-prompt-card">
              Enter your API key to start using Foqus with your Cloud BYOK plan.{" "}
              <button type="button" className="inline-link-button" onClick={() => setShowByokInfoDialog(true)}>
                What is this?
              </button>
            </div>
          ) : null}
          <div className="settings-form">
            <div className="settings-field">
              <label className="label" htmlFor="openai-api-key">API key</label>
              <div className="password-field">
                <input
                  id="openai-api-key"
                  type={showApiKey ? "text" : "password"}
                  value={settings.openAiApiKey}
                  onChange={(event) => setSettings((current) => ({ ...current, openAiApiKey: event.target.value }))}
                  placeholder="sk-..."
                  autoComplete="off"
                />
                <button type="button" className="password-field-toggle" onClick={() => setShowApiKey((current) => !current)}>
                  {showApiKey ? "Hide" : "Show"}
                </button>
              </div>
            </div>
            <div className="settings-field">
              <label className="label" htmlFor="classifier-model">Classifier model</label>
              <input
                id="classifier-model"
                type="text"
                value={settings.classifierModel}
                onChange={(event) => setSettings((current) => ({ ...current, classifierModel: event.target.value }))}
              />
            </div>
            <div className="settings-inline-actions">
              <button
                type="button"
                onClick={() =>
                  void updateSettings(
                    {
                      openAiApiKey: settings.openAiApiKey,
                      classifierModel: settings.classifierModel
                    },
                    "Settings saved."
                  )
                }
              >
                Save API settings
              </button>
            </div>
            <div className="settings-help">
              <p className="muted">
                Your API key is stored in Chrome&apos;s protected extension storage, isolated from other extensions and websites. It is sent directly
                to the AI provider over HTTPS and is never transmitted to Foqus servers.
              </p>
            </div>
          </div>
        </section>
      ) : null}

      {showByokInfoDialog ? <BYOKInfoDialog onClose={() => setShowByokInfoDialog(false)} /> : null}
    </main>
  );
};

const container = document.getElementById("root");
if (!container) {
  throw new Error("Root container not found.");
}

createRoot(container).render(<SettingsPage />);

import { useEffect, useMemo, useState } from "react";
import { createRoot } from "react-dom/client";
import { sendRuntimeRequest } from "../shared/runtime";
import type { Settings } from "../shared/types";
import { supabase } from "../shared/supabaseClient";
import { saveFocusbotAuthSession, clearFocusbotAuthSession, loadFocusbotAuthSession } from "../shared/focusbotAuth";
import { fetchCurrentUser } from "../shared/apiClient";
import { parseExcludedDomains } from "../shared/url";
import "../ui/styles.css";

const SettingsPage = (): JSX.Element => {
  const [settings, setSettings] = useState<Settings>({
    authMode: "byok",
    openAiApiKey: "",
    classifierModel: "gpt-4o-mini",
    onboardingCompleted: false,
    excludedDomains: []
  });
  const [excludedDomainsInput, setExcludedDomainsInput] = useState("");
  const [status, setStatus] = useState("");
  const [focusbotEmailInput, setFocusbotEmailInput] = useState("");
  const [focusbotStatus, setFocusbotStatus] = useState("");

  useEffect(() => {
    void (async () => {
      const response = await sendRuntimeRequest<{ settings: Settings }>({ type: "GET_STATE" });
      if (response.ok && response.data) {
        setSettings(response.data.settings);
        setExcludedDomainsInput(response.data.settings.excludedDomains.join("\n"));
      }
    })();
  }, []);

  useEffect(() => {
    if (settings.authMode !== "focusbot-account") {
      return;
    }

    let cancelled = false;
    void (async () => {
      const existing = await loadFocusbotAuthSession();
      if (existing && !cancelled) {
        setSettings((current) => ({
          ...current,
          authMode: "focusbot-account",
          focusbotEmail: existing.email
        }));
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [settings.authMode]);

  const excludedPreview = useMemo(() => parseExcludedDomains(excludedDomainsInput), [excludedDomainsInput]);

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

  return (
    <main className="app-shell">
      <header>
        <h1>FocusBot Settings</h1>
        <p className="muted">Configure your OpenAI key and task-alignment classifier behavior.</p>
      </header>

      <section className="card">
        <h2>Account mode</h2>
        <p className="muted">
          Choose how FocusBot authenticates and runs classifications. You can switch modes later in this settings page.
        </p>
        <div className="stack">
          <label className="radio-row">
            <input
              type="radio"
              name="auth-mode"
              checked={settings.authMode === "byok"}
              onChange={() =>
                setSettings((current) => ({
                  ...current,
                  authMode: "byok"
                }))
              }
            />
            <span>
              <strong>Bring your own OpenAI key</strong>
              <br />
              <span className="muted">
                Your browser uses your OpenAI API key directly. Data is never sent to FocusBot servers.
              </span>
            </span>
          </label>
          <label className="radio-row">
            <input
              type="radio"
              name="auth-mode"
              checked={settings.authMode === "focusbot-account"}
              onChange={() =>
                setSettings((current) => ({
                  ...current,
                  authMode: "focusbot-account"
                }))
              }
            />
            <span>
              <strong>FocusBot account (managed key)</strong>
              <br />
              <span className="muted">
                Sign in with a FocusBot account to use a managed key and enable multi-device sync.
              </span>
            </span>
          </label>
          {settings.authMode === "focusbot-account" ? (
            <div className="stack">
              <label className="label" htmlFor="focusbot-email">
                FocusBot account email
              </label>
              <input
                id="focusbot-email"
                type="email"
                value={focusbotEmailInput}
                onChange={(event) => setFocusbotEmailInput(event.target.value)}
                placeholder="you@example.com"
              />
              <div className="actions-row">
                <button
                  type="button"
                  onClick={async () => {
                    if (!focusbotEmailInput.trim()) {
                      setFocusbotStatus("Enter an email address first.");
                      return;
                    }
                    setFocusbotStatus("Sending magic link...");
                    const { error } = await supabase.auth.signInWithOtp({
                      email: focusbotEmailInput.trim(),
                      options: {
                        shouldCreateUser: true,
                        emailRedirectTo: "http://localhost:5251/auth/callback.html"
                      }
                    });
                    if (error) {
                      setFocusbotStatus(error.message);
                      return;
                    }
                    setFocusbotStatus("Magic link sent. Open the link from this device to finish sign-in.");
                  }}
                >
                  Send magic link
                </button>
                <button
                  type="button"
                  onClick={async () => {
                    await clearFocusbotAuthSession();
                    await supabase.auth.signOut();
                    setSettings((current) => ({
                      ...current,
                      focusbotEmail: undefined
                    }));
                    setFocusbotStatus("Signed out.");
                  }}
                >
                  Sign out
                </button>
              </div>
              {settings.focusbotEmail ? (
                <p className="muted">
                  Currently signed in as <strong>{settings.focusbotEmail}</strong>
                </p>
              ) : null}
              {focusbotStatus ? <p className="muted">{focusbotStatus}</p> : null}
            </div>
          ) : null}
          {settings.authMode === "focusbot-account" && settings.focusbotEmail ? (
            <p className="muted">
              Signed in as <strong>{settings.focusbotEmail}</strong>
            </p>
          ) : null}
        </div>
      </section>

      <section className="card">
        <h2>OpenAI Configuration</h2>
        <label className="label" htmlFor="api-key">
          OpenAI API Key
        </label>
        <input
          id="api-key"
          type="password"
          value={settings.openAiApiKey}
          onChange={(event) => setSettings((current) => ({ ...current, openAiApiKey: event.target.value }))}
          placeholder="sk-..."
          disabled={settings.authMode === "focusbot-account"}
        />

        <label className="label" htmlFor="model">
          Classifier model
        </label>
        <input
          id="model"
          type="text"
          value={settings.classifierModel}
          onChange={(event) => setSettings((current) => ({ ...current, classifierModel: event.target.value }))}
        />

        <label className="label" htmlFor="excluded-domains">
          Excluded domains (comma or newline separated)
        </label>
        <textarea
          id="excluded-domains"
          rows={7}
          value={excludedDomainsInput}
          onChange={(event) => setExcludedDomainsInput(event.target.value)}
          placeholder="localhost
internal.company.com"
        />

        <p className="muted">
          Data disclosure: task text and page URL/title are sent to OpenAI for classification. Page body content is not
          sent. Some URLs can contain sensitive information.
        </p>
        <p className="muted">Excluded domains bypass OpenAI classification and are counted as aligned visits.</p>

        <div className="actions-row">
          <button onClick={() => void save()}>Save settings</button>
          <span className="pill">{excludedPreview.length} excluded domains</span>
        </div>
        {status ? <p className="muted">{status}</p> : null}
      </section>
    </main>
  );
};

const container = document.getElementById("root");
if (!container) {
  throw new Error("Root container not found.");
}

createRoot(container).render(<SettingsPage />);

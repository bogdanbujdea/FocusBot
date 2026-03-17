import { useEffect, useMemo, useState } from "react";
import { createRoot } from "react-dom/client";
import { sendRuntimeRequest } from "../shared/runtime";
import type { Settings } from "../shared/types";
import { supabase } from "../shared/supabaseClient";
import { clearFocusbotAuthSession, loadFocusbotAuthSession } from "../shared/focusbotAuth";
import { parseExcludedDomains } from "../shared/url";
import { validateOpenAiKey } from "../shared/openaiValidation";
import "./settings.css";

const SettingsPage = (): JSX.Element => {
  const allowedModels = useMemo(() => ["gpt-4o-mini", "gpt-5-mini"] as const, []);
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
  const [byokStatus, setByokStatus] = useState("");
  const [isValidatingByok, setIsValidatingByok] = useState(false);
  const [showOpenAiKey, setShowOpenAiKey] = useState(false);

  useEffect(() => {
    void (async () => {
      const response = await sendRuntimeRequest<{ settings: Settings }>({ type: "GET_STATE" });
      if (response.ok && response.data) {
        const nextSettings = response.data.settings;
        setSettings({
          ...nextSettings,
          classifierModel: allowedModels.includes(nextSettings.classifierModel as (typeof allowedModels)[number])
            ? nextSettings.classifierModel
            : "gpt-4o-mini"
        });
        setExcludedDomainsInput(nextSettings.excludedDomains.join("\n"));
        setFocusbotEmailInput(nextSettings.focusbotEmail ?? "");
      }
    })();
  }, [allowedModels]);

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
        setFocusbotEmailInput(existing.email);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [settings.authMode]);

  // Reactively pick up tokens stored by the content script after
  // the magic-link callback page posts them via window.postMessage.
  useEffect(() => {
    const onChanged = (changes: { [key: string]: chrome.storage.StorageChange }) => {
      if (changes["focusbot.supabaseAccessToken"] || changes["focusbot.supabaseEmail"]) {
        void (async () => {
          const session = await loadFocusbotAuthSession();
          if (session) {
            setSettings((current) => ({
              ...current,
              authMode: "focusbot-account",
              focusbotEmail: session.email
            }));
            setFocusbotEmailInput(session.email);
            setFocusbotStatus(`Signed in as ${session.email}`);
          }
        })();
      }
    };
    chrome.storage.local.onChanged.addListener(onChanged);
    return () => chrome.storage.local.onChanged.removeListener(onChanged);
  }, []);

  const excludedPreview = useMemo(() => parseExcludedDomains(excludedDomainsInput), [excludedDomainsInput]);

  const goBack = (): void => {
    if (window.history.length > 1) {
      window.history.back();
      return;
    }

    window.close();
  };

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
    <main className="app-shell settings-page">
      <header>
        <div className="settings-header-row">
          <div className="settings-header-text">
            <h1>FocusBot Settings</h1>
            <p className="muted">Configure your OpenAI key and task-alignment classifier behavior.</p>
          </div>
          <button type="button" className="settings-back-button" onClick={goBack}>
            Back
          </button>
        </div>
      </header>

      <section className="card settings-section">
        <div className="settings-section-title">
          <h2>Account mode</h2>
          <p className="muted">
            Choose how FocusBot authenticates and runs classifications. You can switch modes later in this settings page.
          </p>
        </div>

        <div className="settings-auth-mode">
          <div className="settings-auth-cards" role="radiogroup" aria-label="Account mode">
            <label className="settings-radio-card" data-selected={settings.authMode === "byok"}>
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
                <span className="settings-radio-card-title">Bring your own OpenAI key</span>
                <span className="settings-radio-card-desc">
                  Your browser uses your OpenAI API key directly. Data is never sent to FocusBot servers.
                </span>
              </span>
            </label>

            <label className="settings-radio-card" data-selected={settings.authMode === "focusbot-account"}>
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
                <span className="settings-radio-card-title">FocusBot account (managed key)</span>
                <span className="settings-radio-card-desc">
                  Sign in with a FocusBot account to use a managed key and enable multi-device sync.
                </span>
              </span>
            </label>
          </div>

          {settings.authMode === "byok" ? (
            <div className="settings-form">
              <div className="settings-field">
                <label className="label" htmlFor="api-key">
                  OpenAI API Key
                </label>
                <input
                  id="api-key"
                  type={showOpenAiKey ? "text" : "password"}
                  value={settings.openAiApiKey}
                  onChange={(event) => {
                    setByokStatus("");
                    setSettings((current) => ({ ...current, openAiApiKey: event.target.value }));
                  }}
                  placeholder="sk-..."
                />
                <div className="settings-inline-actions">
                  <button type="button" onClick={() => setShowOpenAiKey((current) => !current)}>
                    {showOpenAiKey ? "Hide" : "Reveal"}
                  </button>
                  <button
                    type="button"
                    disabled={!settings.openAiApiKey.trim()}
                    onClick={async () => {
                      setByokStatus("");
                      if (!settings.openAiApiKey.trim()) {
                        setByokStatus("Please enter an API key.");
                        return;
                      }
                      try {
                        await navigator.clipboard.writeText(settings.openAiApiKey);
                        setByokStatus("API key copied to clipboard.");
                      } catch {
                        setByokStatus("Unable to copy to clipboard. Please reveal the key and copy it manually.");
                      }
                    }}
                  >
                    Copy
                  </button>
                </div>
              </div>

              <div className="settings-field">
                <label className="label" htmlFor="model">
                  Classifier model
                </label>
                <select
                  id="model"
                  value={
                    allowedModels.includes(settings.classifierModel as (typeof allowedModels)[number])
                      ? (settings.classifierModel as (typeof allowedModels)[number])
                      : "gpt-4o-mini"
                  }
                  onChange={(event) => {
                    setByokStatus("");
                    setSettings((current) => ({ ...current, classifierModel: event.target.value }));
                  }}
                >
                  {allowedModels.map((model) => (
                    <option key={model} value={model}>
                      {model}
                    </option>
                  ))}
                </select>
              </div>

              <div className="settings-inline-actions">
                <button
                  type="button"
                  disabled={isValidatingByok}
                  onClick={async () => {
                    setByokStatus("");
                    if (!settings.openAiApiKey.trim()) {
                      setByokStatus("Please enter an API key.");
                      return;
                    }

                    setIsValidatingByok(true);
                    try {
                      const result = await validateOpenAiKey({
                        apiKey: settings.openAiApiKey.trim(),
                        model: settings.classifierModel
                      });
                      setByokStatus(result.ok ? "API key validated." : result.error ?? "Unable to validate API key.");
                    } catch (error) {
                      setByokStatus(error instanceof Error ? error.message : "Unable to validate API key.");
                    } finally {
                      setIsValidatingByok(false);
                    }
                  }}
                >
                  Validate key
                </button>
              </div>

              {byokStatus ? <p className="muted">{byokStatus}</p> : null}
            </div>
          ) : null}

          {settings.authMode === "focusbot-account" ? (
            <div className="settings-form">
              <div className="settings-field">
                <label className="label" htmlFor="focusbot-email">
                  FocusBot account email
                </label>
                <input
                  id="focusbot-email"
                  type="email"
                  value={focusbotEmailInput}
                  onChange={(event) => setFocusbotEmailInput(event.target.value)}
                  placeholder="you@example.com"
                  disabled={Boolean(settings.focusbotEmail)}
                />
              </div>

              <div className="settings-inline-actions">
                {!settings.focusbotEmail ? (
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
                ) : (
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
                      setFocusbotEmailInput("");
                    }}
                  >
                    Sign out
                  </button>
                )}
              </div>

              {settings.focusbotEmail ? (
                <p className="muted">
                  Currently signed in as <strong>{settings.focusbotEmail}</strong>
                </p>
              ) : null}
              {focusbotStatus ? <p className="muted">{focusbotStatus}</p> : null}
            </div>
          ) : null}
        </div>

        <div className="settings-form">
          <div className="settings-field">
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
          </div>

          <div className="settings-help">
            <p className="muted">
              Data disclosure: task text and page URL/title are sent to OpenAI for classification. Page body content is
              not sent. Some URLs can contain sensitive information.
            </p>
            <p className="muted">Excluded domains bypass OpenAI classification and are counted as aligned visits.</p>
          </div>

          <div className="settings-actions">
            <button onClick={() => void save()}>Save settings</button>
            <span className="pill">{excludedPreview.length} excluded domains</span>
          </div>

          {status ? <p className="muted">{status}</p> : null}
        </div>
      </section>
    </main>
  );
};

const container = document.getElementById("root");
if (!container) {
  throw new Error("Root container not found.");
}

createRoot(container).render(<SettingsPage />);

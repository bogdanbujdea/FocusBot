import { useEffect, useMemo, useState } from "react";
import { createRoot } from "react-dom/client";
import { sendRuntimeRequest } from "../shared/runtime";
import type { Settings } from "../shared/types";
import { parseExcludedDomains } from "../shared/url";
import "../ui/styles.css";

const SettingsPage = (): JSX.Element => {
  const [settings, setSettings] = useState<Settings>({
    openAiApiKey: "",
    classifierModel: "gpt-4o-mini",
    onboardingCompleted: false,
    excludedDomains: []
  });
  const [excludedDomainsInput, setExcludedDomainsInput] = useState("");
  const [status, setStatus] = useState("");

  useEffect(() => {
    void (async () => {
      const response = await sendRuntimeRequest<{ settings: Settings }>({ type: "GET_STATE" });
      if (response.ok && response.data) {
        setSettings(response.data.settings);
        setExcludedDomainsInput(response.data.settings.excludedDomains.join("\n"));
      }
    })();
  }, []);

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
        <p className="muted">Configure your OpenAI key and deep work classifier behavior.</p>
      </header>

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

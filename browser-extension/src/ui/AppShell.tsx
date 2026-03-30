import { useState } from "react";
import { sendRuntimeRequest } from "../shared/runtime";
import { getWebAppAnalyticsUrl } from "../shared/webAppUrl";
import type { RuntimeState } from "../shared/types";
import { getWebAppBillingUrl } from "../shared/webAppUrl";
import { getPlanLabelFromServerPlanType, isByokKeyMissing, isExpiredTrial, isTrialBannerVisible } from "../shared/subscription";
import { supabase } from "../shared/supabaseClient";
import { BYOKInfoDialog } from "./BYOKInfoDialog";
import { SessionCard } from "./SessionCard";
import { SummaryCard } from "./SummaryCard";

interface AppShellProps {
  title: string;
  description: string;
  state: RuntimeState;
  loading: boolean;
  compact?: boolean;
  refreshState: () => Promise<void>;
  showHeaderMeta?: boolean;
}

type ShellStatus = "aligned" | "distracting" | null;
const billingUrl = getWebAppBillingUrl();

const formatTrialRemaining = (trialEndsAt: string | undefined): string => {
  if (!trialEndsAt) return "";
  const endMs = Date.parse(trialEndsAt);
  if (Number.isNaN(endMs)) return "";
  const diffMs = endMs - Date.now();
  if (diffMs <= 0) return "ending soon";

  const totalMinutes = Math.ceil(diffMs / (60 * 1000));
  if (totalMinutes < 60) return `${totalMinutes}m`;
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  if (minutes === 0) return `${hours}h`;
  return `${hours}h ${minutes}m`;
};

const getShellStatus = (state: RuntimeState): ShellStatus => {
  const active = state.activeSession;
  if (!active) return null;
  if (active.pausedAt) return null;

  const visit = active.currentVisit;
  if (!visit) return null;
  if (visit.visitState === "classifying") return null;
  if (visit.visitState === "error") return "distracting";
  if (visit.classification === "neutral") return null;
  return visit.classification ?? null;
};

export const AppShell = ({
  title,
  description,
  state,
  loading,
  compact = false,
  refreshState,
  showHeaderMeta = true
}: AppShellProps): JSX.Element => {
  const [showAccountPanel, setShowAccountPanel] = useState(false);
  const [showByokInfoDialog, setShowByokInfoDialog] = useState(false);
  const [focusbotEmailInput, setFocusbotEmailInput] = useState("");
  const [focusbotStatus, setFocusbotStatus] = useState("");
  const shellStatus = getShellStatus(state);
  const statusClass =
    shellStatus === "aligned" ? "ui-status-aligned" : shellStatus === "distracting" ? "ui-status-distracting" : "";
  const showTrialBanner = isTrialBannerVisible(state.settings, compact);
  const trialRemaining = formatTrialRemaining(state.settings.trialEndsAt);
  const trialExpired = isExpiredTrial(
    state.settings.subscriptionStatus,
    state.settings.serverPlanType,
    state.settings.trialEndsAt
  );
  const planLabel = trialExpired
    ? "No active plan"
    : getPlanLabelFromServerPlanType(state.settings.serverPlanType);
  const accountEmail = state.settings.focusbotEmail ?? "Not signed in";
  const isSignedIn = state.isAuthenticated;
  const showByokBanner = compact && isByokKeyMissing(state.settings, isSignedIn);

  const handleSignOut = async (): Promise<void> => {
    await sendRuntimeRequest({ type: "SIGN_OUT" });
    await refreshState();
    setShowAccountPanel(false);
  };

  const handleSendMagicLink = async (): Promise<void> => {
    const email = focusbotEmailInput.trim();
    if (!email) {
      setFocusbotStatus("Enter an email address first.");
      return;
    }
    setFocusbotStatus("Sending magic link...");
    const { error } = await supabase.auth.signInWithOtp({
      email,
      options: {
        shouldCreateUser: true,
        emailRedirectTo: chrome.runtime.getURL("auth-callback.html")
      }
    });
    if (error) {
      setFocusbotStatus(error.message);
      return;
    }
    setFocusbotStatus("Magic link sent. Open it on this browser profile to finish sign-in.");
  };

  return (
    <main className={`app-shell ${compact ? "compact" : ""} ${statusClass}`}>
    <header className="app-shell-header">
      <div className="app-shell-header-text">
        <h1>{title}</h1>
        {showHeaderMeta && description ? <p className="muted">{description}</p> : null}
      </div>
      <div className="quick-actions">
          {!state.isAuthenticated ? (
            <button
              type="button"
              className="quick-action-btn quick-action-btn--signin"
              onClick={() => setShowAccountPanel(true)}
              title="Sign in"
            >
              Sign in
            </button>
          ) : null}
          <button
            type="button"
            className="quick-action-btn quick-action-btn--analytics"
            onClick={() => {
              void chrome.tabs.create({ url: getWebAppAnalyticsUrl() });
            }}
            title="View Analytics"
          >
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M18 20V10" />
              <path d="M12 20V4" />
              <path d="M6 20v-6" />
            </svg>
          </button>
          <button
            type="button"
            className="quick-action-btn quick-action-btn--settings"
            onClick={() => setShowAccountPanel((current) => !current)}
            title="Account"
          >
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <circle cx="12" cy="12" r="3" />
              <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z" />
            </svg>
          </button>
        </div>
    </header>

    {loading ? <p className="muted">Loading current session...</p> : null}
    {state.lastError ? (
      <section className="card">
        <h2>Attention</h2>
        <p className="error">{state.lastError}</p>
        <div className="actions-row">
          <button onClick={() => void sendRuntimeRequest({ type: "CLEAR_ERROR" })}>Dismiss error</button>
        </div>
      </section>
    ) : null}
    {compact && showAccountPanel ? (
      <section className="popup-overlay" role="dialog" aria-modal="true" aria-label="Account">
        <div className="popup-overlay-card">
          <div className="popup-overlay-header">
            <h2>Account</h2>
            <button type="button" className="popup-overlay-close" onClick={() => setShowAccountPanel(false)}>
              Close
            </button>
          </div>
          {isSignedIn ? (
            <>
              <div className="popup-account-meta">
                <p className="muted">
                  Signed in as <strong>{accountEmail}</strong>
                </p>
                <p className="muted">
                  <strong>Current plan:</strong> {planLabel}
                </p>
              </div>
              <div className="actions-row popup-account-actions">
                <a href={billingUrl} target="_blank" rel="noopener noreferrer">
                  Manage subscription
                </a>
                <button type="button" onClick={() => void handleSignOut()}>
                  Sign out
                </button>
              </div>
            </>
          ) : (
            <div className="settings-form popup-signin-form">
              <div className="settings-field">
                <label className="label" htmlFor="popup-focusbot-email">Email</label>
                <input
                  id="popup-focusbot-email"
                  type="email"
                  value={focusbotEmailInput}
                  onChange={(event) => setFocusbotEmailInput(event.target.value)}
                  placeholder="you@example.com"
                />
              </div>
              <div className="actions-row popup-signin-actions">
                <button type="button" onClick={() => void handleSendMagicLink()}>
                  Send magic link
                </button>
              </div>
              {focusbotStatus ? <p className="muted">{focusbotStatus}</p> : null}
            </div>
          )}
        </div>
      </section>
    ) : null}
    {showTrialBanner ? (
      <section className="trial-banner" aria-live="polite">
        <p>
          Trial ends {trialRemaining}
        </p>
        <a href={billingUrl} target="_blank" rel="noopener noreferrer">
          Manage plan
        </a>
      </section>
    ) : null}
    {showByokBanner ? (
      <section className="byok-banner" aria-live="polite">
        <p>OpenAI API key required for Cloud BYOK.</p>
        <div className="byok-banner-actions">
          <button type="button" className="byok-banner-link" onClick={() => setShowByokInfoDialog(true)}>
            Learn more
          </button>
          <button type="button" className="byok-banner-link" onClick={() => void sendRuntimeRequest({ type: "OPEN_OPTIONS" })}>
            Open settings
          </button>
        </div>
      </section>
    ) : null}

    {showByokInfoDialog ? <BYOKInfoDialog onClose={() => setShowByokInfoDialog(false)} /> : null}

    <SessionCard state={state} compact={compact} onChanged={refreshState} />
    <SummaryCard state={state} />
    </main>
  );
};

import { type FormEvent, useId, useMemo, useState } from "react";
import appIcon from "../../FocusBot.App/Assets/1080.png";
import "./App.css";

type Feature = {
  title: string;
  description: string;
};

type Step = {
  title: string;
  description: string;
};

const FEATURES: Feature[] = [
  {
    title: "Task-aware alignment (AI)",
    description:
      "Foqus evaluates what you’re doing against your task — so the same app can be aligned for one block and distracting for another."
  },
  {
    title: "Catch drift before it breaks the block",
    description:
      "Foqus gives you a clear signal when your current app or site stops matching the task you set, so you can return before small detours turn into fragmented work."
  },
  {
    title: "Analytics that reveal your triggers",
    description:
      "See which sites and apps break your blocks most often, how fragmented your sessions become, and how long it takes to settle back into focused work."
  }
];

const STEPS: Step[] = [
  { title: "Set your task", description: "Tell Foqus what you’re trying to finish right now." },
  { title: "Work normally", description: "Foqus quietly checks whether your activity stays aligned across websites and Windows apps." },
  { title: "Learn from the block", description: "Review what caused context switches so your next block stays cleaner." }
];

function App() {
  const emailId = useId();
  const [email, setEmail] = useState("");
  const [submittedEmail, setSubmittedEmail] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const canSubmit = useMemo(() => email.trim().length > 3 && email.includes("@"), [email]);

  const onSubmit = async (event: FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();
    if (!canSubmit) return;
    if (isSubmitting) return;

    setIsSubmitting(true);
    setSubmitError(null);

    const normalizedEmail = email.trim();

    try {
      const form = event.currentTarget;
      const formData = new FormData(form);
      const company = String(formData.get("company") ?? "");

      // Production safety net: if the build-time env var is missing, default to the deployed API host.
      // In dev, prefer relative URLs unless explicitly configured.
      const apiBase = (import.meta.env.VITE_FOQUS_API_BASE as string | undefined) ?? (import.meta.env.PROD ? "https://api.foqus.me" : "");
      const endpoint = `${apiBase}/api/waitlist`;

      const response = await fetch(endpoint, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email: normalizedEmail, company })
      });

      if (!response.ok) {
        setSubmitError("Could not join the waitlist right now. Please try again in a moment.");
        return;
      }

      setSubmittedEmail(normalizedEmail);
      setEmail("");
      form.reset();
    } catch {
      setSubmitError("Could not join the waitlist right now. Please try again in a moment.");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="landing">
      <header className="landing-header">
        <nav className="landing-nav" aria-label="Primary">
          <div className="landing-brand" aria-label="Foqus">
            <img className="landing-icon" src={appIcon} width={28} height={28} alt="Foqus" />
            <span className="landing-logo" aria-hidden="true">
              Foqus
            </span>
          </div>
          <div className="landing-nav-meta">
            <span className="pill" aria-label="Coming soon">
              Coming soon
            </span>
          </div>
        </nav>
      </header>

      <main className="landing-main">
        <section className="landing-hero" aria-labelledby="hero-title">
          <div className="landing-hero-inner">
            <div className="landing-hero-left">
              <div className="landing-hero-copy">
                <h1 id="hero-title">Protect your focus block</h1>
                <p className="landing-lede">
                  Time block one task. Foqus quietly detects when your work starts to drift across apps and websites, so you can get back before the block
                  is lost.
                </p>
                <div className="landing-hero-actions">
                  <a className="btn btn-primary" href="#waitlist">
                    Get early access
                  </a>
                  <a className="btn btn-secondary" href="#how-it-works">
                    How it works
                  </a>
                </div>
                <p className="landing-note">
                  Passive by default. Task-aware. Built for deep work.
                </p>
              </div>

              <div className="hero-built-for" aria-label="Built for focused blocks">
                <h3 className="hero-built-for-title">Built for focused blocks</h3>
                <div className="role-grid role-grid-compact" aria-label="Work types">
                  <span className="role-pill">Coding</span>
                  <span className="role-pill">Writing</span>
                  <span className="role-pill">Planning</span>
                  <span className="role-pill">Research</span>
                  <span className="role-pill">Strategy</span>
                  <span className="role-pill">Design</span>
                </div>
              </div>

              <div className="landing-hero-why-foqus">
                <h3 className="why-foqus-title">Why Foqus</h3>
                <ul className="why-foqus-list">
                  <li className="why-foqus-item">
                    <span className="why-foqus-bullet" aria-hidden="true">●</span>
                    <span className="why-foqus-text">Catch drift before the block is diluted</span>
                  </li>
                  <li className="why-foqus-item">
                    <span className="why-foqus-bullet" aria-hidden="true">●</span>
                    <span className="why-foqus-text">Task-aware across Windows apps + websites</span>
                  </li>
                </ul>
              </div>
            </div>

            <div className="landing-hero-preview" aria-label="Status preview">
              <div className="preview-card">
                <div className="preview-card-header">
                  <span className="preview-title">Current Focus Session</span>
                  <span className="preview-pill preview-pill-aligned">Aligned</span>
                </div>
                <div className="preview-context">
                  <span className="preview-context-label">Current website</span>
                  <span className="preview-context-value">notion.so</span>
                </div>
                <div className="preview-grid">
                  <div className="preview-metric">
                    <span className="preview-metric-label">Task</span>
                    <span className="preview-metric-value">Write the Q2 marketing plan</span>
                  </div>
                  <div className="preview-metric">
                    <span className="preview-metric-label">Focus</span>
                    <span className="preview-metric-value preview-metric-value-aligned">86%</span>
                  </div>
                  <div className="preview-metric">
                    <span className="preview-metric-label">Focus time</span>
                    <span className="preview-metric-value">11:40</span>
                  </div>
                  <div className="preview-metric">
                    <span className="preview-metric-label">Distracted time</span>
                    <span className="preview-metric-value">01:54</span>
                  </div>
                  <div className="preview-metric">
                    <span className="preview-metric-label">Context switches</span>
                    <span className="preview-metric-value">3</span>
                  </div>
                  <div className="preview-metric">
                    <span className="preview-metric-label">Avg recovery time</span>
                    <span className="preview-metric-value">00:38</span>
                  </div>
                </div>
                <div className="fragmentation" aria-label="Fragmentation">
                  <div className="fragmentation-header">
                    <span className="fragmentation-label">Fragmentation</span>
                  </div>
                  <div
                    className="fragmentation-bar"
                    role="img"
                    aria-label="Time fragmentation: 86 percent aligned, 14 percent distracted"
                  >
                    <span className="fragmentation-segment fragmentation-segment-aligned" style={{ width: "22%" }}>
                      <span className="fragmentation-sr">Aligned segment</span>
                    </span>
                    <span className="fragmentation-segment fragmentation-segment-distracted" style={{ width: "4%" }}>
                      <span className="fragmentation-sr">Distracted segment</span>
                    </span>
                    <span className="fragmentation-segment fragmentation-segment-aligned" style={{ width: "18%" }}>
                      <span className="fragmentation-sr">Aligned segment</span>
                    </span>
                    <span className="fragmentation-segment fragmentation-segment-distracted" style={{ width: "5%" }}>
                      <span className="fragmentation-sr">Distracted segment</span>
                    </span>
                    <span className="fragmentation-segment fragmentation-segment-aligned" style={{ width: "20%" }}>
                      <span className="fragmentation-sr">Aligned segment</span>
                    </span>
                    <span className="fragmentation-segment fragmentation-segment-distracted" style={{ width: "5%" }}>
                      <span className="fragmentation-sr">Distracted segment</span>
                    </span>
                    <span className="fragmentation-segment fragmentation-segment-aligned" style={{ width: "26%" }}>
                      <span className="fragmentation-sr">Aligned segment</span>
                    </span>
                  </div>
                  <div className="fragmentation-legend" aria-label="Legend">
                    <div className="fragmentation-legend-item">
                      <span className="fragmentation-dot fragmentation-dot-aligned" aria-hidden="true" />
                      <span className="fragmentation-legend-text">Aligned 86%</span>
                    </div>
                    <div className="fragmentation-legend-item">
                      <span className="fragmentation-dot fragmentation-dot-distracted" aria-hidden="true" />
                      <span className="fragmentation-legend-text">Distracted 14%</span>
                    </div>
                  </div>
                </div>
                <div className="status-line">
                  <span className="status-accent status-accent-aligned" aria-hidden="true" />
                  <p className="status-text">
                    <strong>AI Reason:</strong> The window is Notion, a planning and documentation tool that directly supports writing a marketing plan.
                  </p>
                </div>
              </div>
            </div>
          </div>
        </section>

        <section className="landing-section" aria-labelledby="not-timer-title">
          <div className="landing-section-header">
            <h2 id="not-timer-title">Not another pomodoro timer</h2>
            <p className="muted">
              Foqus doesn’t just count time. It uses AI to understand your task and checks whether your current app or website still matches what you
              intended to do — without you manually maintaining whitelists.
            </p>
          </div>
          <article className="card">
            <h3 className="card-title">Example: intent-aware, not rule-based</h3>
            <div className="compare">
              <div className="compare-panel">
                <div className="compare-header">
                  <span className="compare-title">Traditional apps</span>
                  <span className="compare-badge">Rule-based</span>
                </div>
                <ul className="compare-list">
                  <li>Needs per-task allow / block lists</li>
                  <li>Makes generic assumptions about apps and categories</li>
                  <li>Can nag or guilt you when you “break the rules”</li>
                </ul>
              </div>
              <div className="compare-panel compare-panel-foqus">
                <div className="compare-header">
                  <span className="compare-title">Foqus</span>
                  <span className="compare-badge compare-badge-foqus">AI task-aware</span>
                </div>
                <ul className="compare-list">
                  <li>No manual whitelisting per task</li>
                  <li>Evaluates alignment based on your intent</li>
                  <li>Supports deep work and intentional breaks</li>
                </ul>
              </div>
            </div>
          </article>
        </section>

        <section className="landing-section" aria-labelledby="features-title">
          <div className="landing-section-header">
            <h2 id="features-title">Built for real focused work</h2>
            <p className="muted">Glassy UI, subtle borders, and information you can scan at a glance.</p>
          </div>
          <div className="card-grid">
            {FEATURES.map((f) => (
              <article key={f.title} className="card">
                <h3 className="card-title">{f.title}</h3>
                <p className="muted">{f.description}</p>
              </article>
            ))}
          </div>
        </section>

        <section id="how-it-works" className="landing-section" aria-labelledby="how-title">
          <div className="landing-section-header">
            <h2 id="how-title">How it works</h2>
            <p className="muted">Three steps. No ceremony.</p>
          </div>
          <ol className="step-grid">
            {STEPS.map((s, index) => (
              <li key={s.title} className="step-card">
                <div className="step-number" aria-hidden="true">
                  {index + 1}
                </div>
                <div className="step-content">
                  <h3 className="card-title">{s.title}</h3>
                  <p className="muted">{s.description}</p>
                </div>
              </li>
            ))}
          </ol>
        </section>

        <section id="waitlist" className="landing-section landing-cta" aria-labelledby="cta-title">
          <div className="landing-section-header">
            <h2 id="cta-title">Get early access to Foqus</h2>
            <p className="muted">Join the waitlist for early access when Foqus launches.</p>
          </div>

          <div className="cta-card card">
            <form className="waitlist-form" onSubmit={onSubmit}>
              <label className="label" htmlFor={emailId}>
                Email
              </label>
              <div className="waitlist-row">
                <input
                  id={emailId}
                  name="email"
                  type="email"
                  inputMode="email"
                  autoComplete="email"
                  placeholder="you@company.com"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  disabled={isSubmitting}
                />
                <input
                  className="waitlist-honeypot"
                  tabIndex={-1}
                  aria-hidden="true"
                  autoComplete="off"
                  name="company"
                  type="text"
                  defaultValue=""
                />
                <button type="submit" disabled={!canSubmit || isSubmitting}>
                  {isSubmitting ? "Joining..." : "Get early access"}
                </button>
              </div>
              <p className="trust-note muted">One email when it launches. No spam.</p>
              {submittedEmail ? (
                <p className="muted" role="status">
                  Thanks — check your inbox to confirm <strong>{submittedEmail}</strong>.
                </p>
              ) : submitError ? (
                <p className="muted" role="status">
                  {submitError}
                </p>
              ) : (
                <p className="muted" role="status">
                  For people who want cleaner time blocks, fewer detours, and more meaningful work done.
                </p>
              )}
            </form>
          </div>
        </section>
      </main>

      <footer className="landing-footer">
        <div className="landing-footer-inner">
          <span className="landing-footer-brand">Foqus</span>
          <span className="muted">© {new Date().getFullYear()}</span>
        </div>
      </footer>
    </div>
  );
}

export default App;

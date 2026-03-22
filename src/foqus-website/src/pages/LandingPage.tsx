import { type FormEvent, useId, useMemo, useState } from "react";
import appIcon from "../../../FocusBot.App/Assets/1080.png";

type Step = {
  title: string;
  description: string;
};

type FoqusWorkExample = {
  id: string;
  persona: string;
  sameTaskAsPrevious?: boolean;
  task: string;
  /** When false, task string is shown as-is (e.g. inner quotes on a phrase). Otherwise wrapped in outer quotation marks. */
  taskEncloseInQuotes?: boolean;
  where: {
    surface: "browser" | "desktop";
    label: string;
    detail?: string;
  };
  verdict: "focused" | "distracted";
  why: string;
};

const FOQUS_WORK_EXAMPLES: FoqusWorkExample[] = [
  {
    id: "writer-facebook",
    persona: "Writer",
    task: "Find inspiration for the \u201cSocial Media in 2026\u201d book",
    taskEncloseInQuotes: false,
    where: { surface: "browser", label: "facebook.com" },
    verdict: "focused",
    why: "Social feeds match the book topic."
  },
  {
    id: "writer-excel",
    persona: "Writer",
    sameTaskAsPrevious: true,
    task: "Find inspiration for the \u201cSocial Media in 2026\u201d book",
    taskEncloseInQuotes: false,
    where: { surface: "desktop", label: "Excel", detail: "Monthly budget" },
    verdict: "distracted",
    why: "Personal finance is not book research."
  },
  {
    id: "dev-youtube-fun",
    persona: "Programmer",
    task: "Fix the major bug today",
    where: { surface: "browser", label: "youtube.com", detail: "Funny video" },
    verdict: "distracted",
    why: "Entertainment, not the bug."
  },
  {
    id: "dev-youtube-learn",
    persona: "Programmer",
    sameTaskAsPrevious: true,
    task: "Fix the major bug today",
    where: { surface: "browser", label: "youtube.com", detail: "JavaScript tutorial" },
    verdict: "focused",
    why: "Learning that can unblock the fix."
  }
];

const ANTI_POSITIONING: { headline: string; body: string }[] = [
  {
    headline: "It doesn't block anything.",
    body: "Foqus gives you a signal when you drift. You decide what to do with it."
  },
  {
    headline: "It doesn't assume YouTube is bad.",
    body: "Every app and page is evaluated against your current task, not a hardcoded category list."
  },
  {
    headline: "It doesn't guilt you.",
    body: "Focus score is feedback, not punishment. Take a break when you need one."
  }
];

const STEPS: Step[] = [
  {
    title: "Name your task",
    description: 'Examples: "Write the Q2 plan", "Review pull requests", "Take a break".'
  },
  {
    title: "Work naturally",
    description: "Foqus watches your browser tabs and Windows apps — one session, full picture."
  },
  {
    title: "Get the signal",
    description:
      "Foqus shows Focused or Distracted when you drift (you do not set those labels yourself), plus a session summary when you are done."
  }
];

type WaitlistSignupFormProps = {
  formId?: string;
  emailFieldId: string;
  className?: string;
  submitButtonClassName?: string;
  email: string;
  onEmailChange: (value: string) => void;
  onSubmit: (event: FormEvent<HTMLFormElement>) => Promise<void>;
  isSubmitting: boolean;
  canSubmit: boolean;
  submittedEmail: string | null;
  submitError: string | null;
};

function WaitlistSignupForm({
  formId,
  emailFieldId,
  className,
  submitButtonClassName,
  email,
  onEmailChange,
  onSubmit,
  isSubmitting,
  canSubmit,
  submittedEmail,
  submitError
}: WaitlistSignupFormProps) {
  return (
    <form id={formId} className={className} onSubmit={onSubmit} aria-label="Join the Foqus waitlist">
      <label className="label" htmlFor={emailFieldId}>
        Email
      </label>
      <div className="waitlist-row">
        <input
          id={emailFieldId}
          name="email"
          type="email"
          inputMode="email"
          autoComplete="email"
          placeholder="you@company.com"
          value={email}
          onChange={(e) => onEmailChange(e.target.value)}
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
        <button type="submit" className={submitButtonClassName} disabled={!canSubmit || isSubmitting}>
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
  );
}

function FoqusExampleVerdictBadge({ verdict }: { verdict: "focused" | "distracted" }) {
  const label = verdict === "focused" ? "Focused" : "Distracted";
  const pillClass = verdict === "focused" ? "preview-pill preview-pill-aligned" : "preview-pill preview-pill-distracted";
  return (
    <span className={`foqus-verdict-badge ${pillClass}`} aria-label={`Foqus: ${label}`}>
      {label}
    </span>
  );
}

export function LandingPage() {
  const emailIdHero = useId();
  const emailIdFooter = useId();
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

      <main className="landing-main landing-main--wide">
        <section className="landing-hero" aria-labelledby="hero-title">
          <div className="landing-hero-inner">
            <div className="landing-hero-left">
              <div className="landing-hero-copy">
                <h1 id="hero-title">The first focus tool that understands what you are working on.</h1>
                <p className="landing-lede">
                  Foqus uses AI to evaluate whether your current app or website actually matches your task — across your desktop and browser, in one session.
                  No whitelists, no assumptions, no nagging.
                </p>
                <div className="landing-hero-waitlist">
                  <WaitlistSignupForm
                    formId="waitlist-hero"
                    emailFieldId={emailIdHero}
                    className="waitlist-form waitlist-form--hero"
                    submitButtonClassName="btn btn-primary"
                    email={email}
                    onEmailChange={setEmail}
                    onSubmit={onSubmit}
                    isSubmitting={isSubmitting}
                    canSubmit={canSubmit}
                    submittedEmail={submittedEmail}
                    submitError={submitError}
                  />
                </div>
              </div>
            </div>

            <div className="landing-hero-preview" aria-label="Status preview">
              <div className="preview-card">
                <div className="preview-card-header">
                  <div className="preview-card-header-text">
                    <span className="preview-title">Current Focus Session</span>
                    <span className="preview-card-subtitle">
                      You choose the task for the block. Foqus sets Focused or Distracted from what you are actually on — not something you toggle.
                    </span>
                  </div>
                  <div className="preview-classification-block">
                    <span className="preview-classification-label">Foqus</span>
                    <span className="preview-pill preview-pill-aligned">Focused</span>
                  </div>
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
                <div className="focus-split" aria-label="Focus time split">
                  <div className="focus-split-header">
                    <span className="focus-split-label">Focus split</span>
                  </div>
                  <div
                    className="focus-split-bar"
                    role="progressbar"
                    aria-valuemin={0}
                    aria-valuemax={100}
                    aria-valuenow={86}
                    aria-label="Session focus: 86 percent focused, 14 percent distracted"
                  >
                    <div className="focus-split-progress-fill" style={{ width: "86%" }} />
                  </div>
                  <div className="focus-split-legend" aria-label="Focused and distracted percentages">
                    <div className="focus-split-legend-item">
                      <span className="focus-split-dot focus-split-dot-focused" aria-hidden="true" />
                      <span className="focus-split-legend-text">Focused 86%</span>
                    </div>
                    <div className="focus-split-legend-item">
                      <span className="focus-split-dot focus-split-dot-distracted" aria-hidden="true" />
                      <span className="focus-split-legend-text">Distracted 14%</span>
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

        <section className="landing-section landing-section--examples" aria-labelledby="examples-title">
          <div className="landing-section-header landing-section-header--xl">
            <h2 id="examples-title">Examples of working with Foqus</h2>
            <p className="muted landing-section-lede">
              Same person, same task — different tab or app, different call. Foqus compares what you are doing to the task you set;{" "}
              <strong>Focused</strong> and <strong>Distracted</strong> are its readouts, not buttons you press.
            </p>
          </div>

          <ul className="foqus-examples" aria-label="Four persona examples">
            {FOQUS_WORK_EXAMPLES.map((ex) => (
              <li key={ex.id} className={`foqus-example foqus-example--${ex.verdict}`}>
                <div className="foqus-example-top">
                  <p className="foqus-persona-line">
                    You&rsquo;re a <strong className="foqus-persona-role">{ex.persona}</strong>
                  </p>
                  {ex.sameTaskAsPrevious ? (
                    <span className="foqus-same-task-badge">Same task</span>
                  ) : null}
                </div>
                <p className="foqus-example-task">
                  <span className="foqus-example-task-label">Task</span>
                  <span className="foqus-quote">
                    {ex.taskEncloseInQuotes === false ? (
                      ex.task
                    ) : (
                      <>
                        &ldquo;{ex.task}&rdquo;
                      </>
                    )}
                  </span>
                </p>
                <div className="foqus-example-flow">
                  <div className={`foqus-where foqus-where--${ex.where.surface}`}>
                    <span className="foqus-where-kind">{ex.where.surface === "browser" ? "Browser tab" : "Foreground app"}</span>
                    <span className="foqus-where-label">{ex.where.label}</span>
                    {ex.where.detail ? <span className="foqus-where-detail">{ex.where.detail}</span> : null}
                  </div>
                  <span className="foqus-flow-arrow">&rarr;</span>
                  <FoqusExampleVerdictBadge verdict={ex.verdict} />
                </div>
                <p className="foqus-example-why">{ex.why}</p>
              </li>
            ))}
          </ul>
        </section>

        <section className="coverage-callout" aria-labelledby="coverage-callout-title">
          <h3 id="coverage-callout-title" className="visually-hidden">
            One session across browser and desktop
          </h3>
          <div className="coverage-callout-inner" aria-hidden="true">
            <div className="coverage-node coverage-node--browser">
              <span className="coverage-node-label">Extension</span>
              <span className="coverage-node-sub">Browser tabs</span>
            </div>
            <div className="coverage-join">
              <span className="coverage-join-line" />
              <span className="coverage-join-badge">One session</span>
              <span className="coverage-join-line" />
            </div>
            <div className="coverage-node coverage-node--desktop">
              <span className="coverage-node-label">Windows app</span>
              <span className="coverage-node-sub">Foreground apps</span>
            </div>
          </div>
          <p className="coverage-callout-copy">Browser tabs and desktop apps, tracked together in one focus session.</p>
        </section>

        <section className="landing-section landing-section--anti" aria-labelledby="anti-title">
          <div className="landing-section-header landing-section-header--xl">
            <h2 id="anti-title">Not another blocker</h2>
            <p className="muted landing-section-lede">Foqus is built for people who are tired of being judged by their tools.</p>
          </div>
          <ul className="anti-list">
            {ANTI_POSITIONING.map((item) => (
              <li key={item.headline} className="anti-item">
                <p className="anti-headline">{item.headline}</p>
                <p className="anti-body muted">{item.body}</p>
              </li>
            ))}
          </ul>
        </section>

        <section className="landing-section landing-section--analytics" aria-labelledby="learn-title">
          <div className="landing-section-header landing-section-header--xl">
            <h2 id="learn-title">What you learn from each session</h2>
            <p className="muted landing-section-lede">
              Every block teaches you something. See which apps pull you off track, how fast you recover, and how your focus patterns change over time.
            </p>
          </div>
          <div className="analytics-preview">
            <div className="analytics-preview-grid">
              <div className="preview-metric analytics-metric">
                <span className="preview-metric-label">Focus score</span>
                <span className="preview-metric-value preview-metric-value-aligned">86%</span>
              </div>
              <div className="preview-metric analytics-metric">
                <span className="preview-metric-label">Context switches</span>
                <span className="preview-metric-value">3</span>
              </div>
              <div className="preview-metric analytics-metric">
                <span className="preview-metric-label">Avg recovery</span>
                <span className="preview-metric-value">00:38</span>
              </div>
              <div className="preview-metric analytics-metric analytics-metric--wide">
                <span className="preview-metric-label">Top drift triggers</span>
                <span className="preview-metric-value">Slack, email</span>
              </div>
            </div>
          </div>
        </section>

        <section id="how-it-works" className="landing-section landing-section--steps" aria-labelledby="how-title">
          <div className="landing-section-header landing-section-header--xl">
            <h2 id="how-title">How it works</h2>
            <p className="muted landing-section-lede">Three steps. No ceremony.</p>
          </div>
          <ol className="step-grid">
            {STEPS.map((s, index) => (
              <li key={s.title} className="step-card">
                <div className="step-number" aria-hidden="true">
                  {index + 1}
                </div>
                <div className="step-content">
                  <h3 className="step-title">{s.title}</h3>
                  <p className="muted">{s.description}</p>
                </div>
              </li>
            ))}
          </ol>
        </section>

        <section className="landing-section landing-cta landing-cta--footer" aria-labelledby="cta-title">
          <div className="landing-section-header landing-section-header--center landing-section-header--xl">
            <h2 id="cta-title">Be the first to try a focus tool that actually gets it.</h2>
            <p className="muted landing-section-lede">Join the waitlist for early access when Foqus launches.</p>
          </div>

          <div className="cta-card card cta-card--narrow">
            <WaitlistSignupForm
              formId="waitlist"
              emailFieldId={emailIdFooter}
              className="waitlist-form"
              email={email}
              onEmailChange={setEmail}
              onSubmit={onSubmit}
              isSubmitting={isSubmitting}
              canSubmit={canSubmit}
              submittedEmail={submittedEmail}
              submitError={submitError}
            />
          </div>
        </section>
      </main>
    </div>
  );
}

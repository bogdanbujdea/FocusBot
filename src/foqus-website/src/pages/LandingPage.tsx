import { type FormEvent, useId, useMemo, useState } from "react";
import appIcon from "../../../FocusBot.App/Assets/1080.png";

type Step = {
  title: string;
  description: string;
};

type FoqusHeadIcon = "writer" | "programmer" | "excel";
type FoqusWhereBrand = "facebook" | "excel" | "youtube";

type FoqusWorkExample = {
  id: string;
  persona: string;
  headIcon: FoqusHeadIcon;
  task: string;
  /** When false, task string is shown as-is (e.g. inner quotes on a phrase). Otherwise wrapped in outer quotation marks. */
  taskEncloseInQuotes?: boolean;
  where: {
    surface: "browser" | "desktop";
    label: string;
    detail?: string;
    brand: FoqusWhereBrand;
  };
  verdict: "focused" | "distracted";
  why: string;
  /** Decorative hint for YouTube rows (funny clip vs tutorial). */
  youtubeVariant?: "fun" | "learn";
};

const FOQUS_WORK_EXAMPLES: FoqusWorkExample[] = [
  {
    id: "writer-facebook",
    persona: "Writer",
    headIcon: "writer",
    task: "Find inspiration for the \u201cSocial Media in 2026\u201d book",
    taskEncloseInQuotes: false,
    where: { surface: "browser", label: "facebook.com", brand: "facebook" },
    verdict: "focused",
    why: "Social feeds match the book topic."
  },
  {
    id: "writer-excel",
    persona: "Writer",
    headIcon: "excel",
    task: "Find inspiration for the \u201cSocial Media in 2026\u201d book",
    taskEncloseInQuotes: false,
    where: { surface: "desktop", label: "Excel", detail: "Monthly budget", brand: "excel" },
    verdict: "distracted",
    why: "Personal finance is not book research."
  },
  {
    id: "dev-youtube-fun",
    persona: "Programmer",
    headIcon: "programmer",
    task: "Fix the major bug today",
    where: { surface: "browser", label: "youtube.com", detail: "Funny video", brand: "youtube" },
    verdict: "distracted",
    why: "Entertainment, not the bug.",
    youtubeVariant: "fun"
  },
  {
    id: "dev-youtube-learn",
    persona: "Programmer",
    headIcon: "programmer",
    task: "Fix the major bug today",
    where: { surface: "browser", label: "youtube.com", detail: "JavaScript tutorial", brand: "youtube" },
    verdict: "focused",
    why: "Learning that can unblock the fix.",
    youtubeVariant: "learn"
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

function IconExampleHead({ kind }: { kind: FoqusHeadIcon }) {
  const cls = "foqus-head-icon-svg";
  if (kind === "writer") {
    return (
      <span className="foqus-head-icon foqus-head-icon--writer" aria-hidden="true">
        <svg className={cls} width={26} height={26} viewBox="0 0 24 24" fill="none">
          <path
            d="M12 2C10.5 5 7 6.5 7 11a5 5 0 0 0 10 0c0-4.5-3.5-6-5-9z"
            fill="currentColor"
            opacity={0.85}
          />
          <path d="M12 16v5" stroke="currentColor" strokeWidth={1.8} strokeLinecap="round" />
        </svg>
      </span>
    );
  }
  if (kind === "excel") {
    return (
      <span className="foqus-head-icon foqus-head-icon--excel" aria-hidden="true">
        <svg className={cls} width={26} height={26} viewBox="0 0 24 24" fill="none">
          <rect x={3} y={3} width={18} height={18} rx={3} fill="#217346" />
          <path d="M7 8h4v4H7V8zm6 0h4v4h-4V8zM7 14h4v4H7v-4zm6 0h4v4h-4v-4z" fill="#fff" opacity={0.95} />
        </svg>
      </span>
    );
  }
  return (
    <span className="foqus-head-icon foqus-head-icon--programmer" aria-hidden="true">
      <svg className={cls} width={26} height={26} viewBox="0 0 24 24" fill="none">
        <path
          d="M8 8l-4 4 4 4M16 8l4 4-4 4M14 6l-4 12"
          stroke="currentColor"
          strokeWidth={1.85}
          strokeLinecap="round"
          strokeLinejoin="round"
        />
      </svg>
    </span>
  );
}

function IconWhereBrand({ brand }: { brand: FoqusWhereBrand }) {
  if (brand === "facebook") {
    return (
      <span className="foqus-brand-icon foqus-brand-icon--facebook" aria-hidden="true">
        <svg width={40} height={40} viewBox="0 0 24 24" fill="none">
          <circle cx={12} cy={12} r={11} fill="#1877F2" />
          <path
            fill="#fff"
            d="M15.5 8h-1.4c-1.7 0-2 .9-2 1.7V11h3.4l-.5 2.9H12V22H9v-8.1H7V11h2V9.3c0-2.4 1.2-4 3.8-4 1.1 0 2 .1 2.3.1V8z"
          />
        </svg>
      </span>
    );
  }
  if (brand === "excel") {
    return (
      <span className="foqus-brand-icon foqus-brand-icon--excel" aria-hidden="true">
        <svg width={40} height={40} viewBox="0 0 24 24" fill="none">
          <rect x={2} y={2} width={20} height={20} rx={3} fill="#217346" />
          <path d="M7 7h4v4H7V7zm6 0h4v4h-4V7zM7 13h4v4H7v-4zm6 0h4v4h-4v-4z" fill="#fff" />
        </svg>
      </span>
    );
  }
  return (
    <span className="foqus-brand-icon foqus-brand-icon--youtube" aria-hidden="true">
      <svg width={40} height={40} viewBox="0 0 24 24" fill="none">
        <rect x={2} y={5} width={20} height={14} rx={3} fill="#FF0000" />
        <path d="M10 9.5v5l4.5-2.5L10 9.5z" fill="#fff" />
      </svg>
    </span>
  );
}

function FoqusExampleVerdictBadge({ verdict }: { verdict: "focused" | "distracted" }) {
  const label = verdict === "focused" ? "Focused" : "Distracted";
  if (verdict === "focused") {
    return (
      <span className="foqus-card-verdict foqus-card-verdict--focused" aria-label={`Foqus: ${label}`}>
        <svg className="foqus-card-verdict-glyph" width={14} height={14} viewBox="0 0 24 24" fill="none" aria-hidden="true">
          <path d="M5 13l4 4L19 7" stroke="currentColor" strokeWidth={2.5} strokeLinecap="round" strokeLinejoin="round" />
        </svg>
        {label}
      </span>
    );
  }
  return (
    <span className="foqus-card-verdict foqus-card-verdict--distracted" aria-label={`Foqus: ${label}`}>
      <svg className="foqus-card-verdict-glyph" width={14} height={14} viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
        <path d="M12 2L2 20h20L12 2zm1 15h-2v-2h2v2zm0-4h-2v-4h2v4z" />
      </svg>
      {label}
    </span>
  );
}

function FoqusExampleTaskLinkGlyph() {
  return (
    <svg
      className="foqus-example-why-bridge-svg"
      width={18}
      height={14}
      viewBox="0 0 18 14"
      aria-hidden="true"
    >
      <path
        d="M9 12V3M4 7l5-5 5 5"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.6"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

function FoqusExampleWhyIcon({ verdict }: { verdict: "focused" | "distracted" }) {
  if (verdict === "focused") {
    return (
      <svg className="foqus-why-glyph" width={18} height={18} viewBox="0 0 24 24" fill="none" aria-hidden="true">
        <circle cx={12} cy={12} r={10} stroke="currentColor" strokeWidth={2} />
        <path d="M8 12l3 3 5-6" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" />
      </svg>
    );
  }
  return (
    <svg className="foqus-why-glyph" width={18} height={18} viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <circle cx={12} cy={12} r={10} stroke="currentColor" strokeWidth={2} />
      <path d="M12 8v5M12 16h.01" stroke="currentColor" strokeWidth={2.2} strokeLinecap="round" />
    </svg>
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
          <div className="landing-section-header landing-section-header--xl examples-section-intro">
            <h2 id="examples-title" className="examples-section-title">
              Examples
            </h2>
            <p className="examples-section-subtitle muted">
              See how Foqus classifies your focus depending on what you&rsquo;re working on.
            </p>
          </div>

          <ul className="foqus-examples" aria-label="Four persona examples">
            {FOQUS_WORK_EXAMPLES.map((ex) => (
              <li key={ex.id} className={`foqus-example foqus-example--${ex.verdict}`}>
                <header className="foqus-example-head">
                  <div className="foqus-example-head-left">
                    <IconExampleHead kind={ex.headIcon} />
                    <div className="foqus-example-head-text">
                      <p className="foqus-persona-line">
                        You&rsquo;re a <strong className="foqus-persona-role">{ex.persona}</strong>
                      </p>
                    </div>
                  </div>
                </header>
                <div className="foqus-example-body">
                  <p className="foqus-example-task">
                    <span className="foqus-example-task-label">Your task</span>
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
                  <div className="foqus-example-connector" aria-hidden="true">
                    <span className="foqus-example-connector-line" />
                    <span className="foqus-example-connector-arrow" />
                  </div>
                  <div
                    className={`foqus-where-frame foqus-where-frame--${ex.where.surface}`}
                    aria-label={
                      ex.where.surface === "browser"
                        ? `Browser tab showing ${ex.where.label}`
                        : `Foreground window: ${ex.where.label}`
                    }
                  >
                    {ex.where.surface === "browser" ? (
                      <div className="foqus-where-frame-chrome foqus-where-frame-chrome--browser" aria-hidden="true">
                        <span className="foqus-where-frame-traffic">
                          <span className="foqus-where-frame-dot foqus-where-frame-dot--r" />
                          <span className="foqus-where-frame-dot foqus-where-frame-dot--y" />
                          <span className="foqus-where-frame-dot foqus-where-frame-dot--g" />
                        </span>
                        <span className="foqus-where-frame-tab" />
                      </div>
                    ) : (
                      <div className="foqus-where-frame-chrome foqus-where-frame-chrome--desktop" aria-hidden="true">
                        <span className="foqus-where-frame-win-icon" />
                        <span className="foqus-where-frame-win-title" />
                        <span className="foqus-where-frame-win-btns">
                          <span className="foqus-where-frame-win-btn" />
                          <span className="foqus-where-frame-win-btn" />
                          <span className="foqus-where-frame-win-btn foqus-where-frame-win-btn--close" />
                        </span>
                      </div>
                    )}
                    <div className={`foqus-where-strip foqus-where-strip--${ex.where.brand}`}>
                      <IconWhereBrand brand={ex.where.brand} />
                      <div className="foqus-where-strip-text">
                        <span className="foqus-where-label">{ex.where.label}</span>
                        {ex.where.detail ? <span className="foqus-where-detail">{ex.where.detail}</span> : null}
                      </div>
                      {ex.youtubeVariant === "fun" ? (
                        <div className="foqus-yt-thumb" aria-hidden="true">
                          <span className="foqus-yt-thumb-play" />
                        </div>
                      ) : null}
                    </div>
                  </div>
                </div>
                <footer className={`foqus-example-why foqus-example-why--${ex.verdict}`}>
                  <div className="foqus-example-why-head">
                    <div className="foqus-example-why-label-row">
                      <FoqusExampleTaskLinkGlyph />
                      <span className="foqus-example-why-label">Foqus classification</span>
                    </div>
                    <FoqusExampleVerdictBadge verdict={ex.verdict} />
                  </div>
                  <div className="foqus-example-why-main">
                    <FoqusExampleWhyIcon verdict={ex.verdict} />
                    <span className="foqus-why-copy">{ex.why}</span>
                  </div>
                </footer>
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

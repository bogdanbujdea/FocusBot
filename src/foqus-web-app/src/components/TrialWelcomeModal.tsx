import { useEffect, useRef } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/useAuth";
import "./TrialWelcomeModal.css";

interface TrialWelcomeModalProps {
  trialEndsAt: string;
  onDismiss: () => void;
}

export function TrialWelcomeModal({ trialEndsAt, onDismiss }: TrialWelcomeModalProps) {
  const dialogRef = useRef<HTMLDivElement>(null);

  const formattedEnd = new Date(trialEndsAt).toLocaleString("en-US", {
    dateStyle: "medium",
    timeStyle: "short",
  });

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") onDismiss();
    };
    document.addEventListener("keydown", handleKeyDown);
    dialogRef.current?.focus();
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [onDismiss]);

  return (
    <div className="trial-modal-backdrop" role="presentation" onClick={onDismiss}>
      <div
        ref={dialogRef}
        className="trial-modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby="trial-modal-title"
        tabIndex={-1}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="trial-modal-header">
          <h2 id="trial-modal-title" className="trial-modal-title">
            Welcome to Foqus
          </h2>
        </div>
        <div className="trial-modal-body">
          <p className="trial-modal-intro">
            Foqus helps you stay focused by monitoring your active windows, scoring
            your focus sessions, and syncing your progress across all your devices.
          </p>
          <div className="trial-modal-highlight">
            <span className="trial-modal-highlight-label">Your trial</span>
            <p className="trial-modal-highlight-text">
              You have <strong>24 hours of full access</strong> to all features — no
              credit card required. Trial ends on <strong>{formattedEnd}</strong>.
            </p>
          </div>
          <p className="trial-modal-after">
            After your trial, choose between <strong>Foqus BYOK</strong> (bring your
            own AI key, $1.99/mo) or <strong>Foqus Premium</strong> (fully managed
            AI, $4.99/mo) to keep your data synced and insights flowing.
          </p>
        </div>
        <div className="trial-modal-actions">
          <Link to="/billing" className="trial-modal-plans-link" onClick={onDismiss}>
            Compare plans
          </Link>
          <button
            type="button"
            className="trial-modal-dismiss"
            onClick={onDismiss}
            autoFocus
          >
            Got it, let&apos;s go
          </button>
        </div>
      </div>
    </div>
  );
}

export function trialWelcomeSeenKey(userId: string): string {
  return `foqus.trialWelcomeSeen.${userId}`;
}

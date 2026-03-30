import { useCallback, type MouseEvent } from "react";
import "./BYOKSetupModal.css";

export type BYOKSetupModalProps = {
  open: boolean;
  onDismiss: () => void;
};

export function BYOKSetupModal({ open, onDismiss }: BYOKSetupModalProps) {
  const onBackdropClick = useCallback(
    (e: MouseEvent<HTMLDivElement>) => {
      if (e.target === e.currentTarget) onDismiss();
    },
    [onDismiss]
  );

  if (!open) return null;

  return (
    <div
      className="byok-setup-modal-backdrop"
      role="presentation"
      onMouseDown={onBackdropClick}
    >
      <div
        className="byok-setup-modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby="byok-setup-title"
      >
        <h2 id="byok-setup-title" className="byok-setup-modal-title">
          You&apos;re all set
        </h2>
        <p className="byok-setup-modal-lead">
          To start using Foqus with your own API key:
        </p>
        <ol className="byok-setup-modal-steps">
          <li>Open the Foqus Windows app or browser extension.</li>
          <li>Go to Settings.</li>
          <li>Paste your OpenAI (or provider) API key.</li>
        </ol>
        <p className="byok-setup-modal-note">
          Your API key is encrypted locally on your device and is not stored on our
          servers.
        </p>
        <div className="byok-setup-modal-actions">
          <button type="button" className="byok-setup-modal-primary" onClick={onDismiss}>
            Got it
          </button>
        </div>
      </div>
    </div>
  );
}

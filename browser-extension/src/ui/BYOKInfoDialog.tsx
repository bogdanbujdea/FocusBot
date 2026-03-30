interface BYOKInfoDialogProps {
  onClose: () => void;
}

export const BYOKInfoDialog = ({ onClose }: BYOKInfoDialogProps): JSX.Element => {
  return (
    <section className="popup-overlay byok-info-dialog-overlay" role="dialog" aria-modal="true" aria-label="Cloud BYOK details">
      <div className="popup-overlay-card byok-info-dialog">
        <div className="popup-overlay-header">
          <h2>Cloud BYOK</h2>
          <button type="button" className="popup-overlay-close" onClick={onClose}>
            Close
          </button>
        </div>

        <div className="byok-info-section">
          <h3>What this plan means</h3>
          <p className="muted">
            Cloud BYOK means you use your own OpenAI API key with Foqus. Your key in the Windows app is not shared with the browser extension,
            so you need to add it separately in extension settings.
          </p>
        </div>

        <div className="byok-info-section">
          <h3>Where to add your key</h3>
          <p className="muted">
            Open extension Settings, then paste your OpenAI API key in the API key field. After saving, browser page classification can run under
            your BYOK plan.
          </p>
        </div>

        <div className="byok-info-section">
          <h3>Security</h3>
          <p className="muted">
            Your API key is stored in Chrome&apos;s protected extension storage, isolated from other extensions and websites. It is sent directly to
            the AI provider over HTTPS and is never transmitted to Foqus servers.
          </p>
        </div>
      </div>
    </section>
  );
};

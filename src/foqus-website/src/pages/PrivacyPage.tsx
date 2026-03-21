import { Link } from "react-router-dom";
import "./LegalPage.css";

export function PrivacyPage() {
  return (
    <div className="legal-page">
      <header className="legal-page-header">
        <div className="legal-page-header-inner">
          <Link to="/" className="legal-back-link">
            &larr; Back to Foqus
          </Link>
        </div>
      </header>

      <div className="legal-page-content">
        <h1>Privacy Policy</h1>
        <p className="legal-last-updated">Last updated: March 21, 2026</p>

        <h2>1. Data Controller</h2>
        <p>
          The data controller for the Foqus platform is <strong>SC NeuroQode Solutions SRL</strong>,
          a company registered in Romania. For any privacy-related inquiries, contact us at{" "}
          <a href="mailto:bogdan@neuroqode.ai">bogdan@neuroqode.ai</a>.
        </p>

        <h2>2. Data We Collect</h2>

        <h3>2.1 Account Data</h3>
        <p>
          When you create a Foqus account, we collect your <strong>email address</strong> for
          authentication. We use Supabase for authentication services. No password is stored — we use
          magic link (passwordless) authentication.
        </p>

        <h3>2.2 Focus Session Data</h3>
        <p>For cloud users, we collect and store on our servers:</p>
        <ul>
          <li>Focus task descriptions you set for each session</li>
          <li>Session start/end times, duration, and focus scores</li>
          <li>Classification results (aligned, not aligned, unclear) with timestamps</li>
          <li>Context metadata sent for classification: application/process names, window titles, page URLs, and page titles</li>
          <li>Aggregated analytics: aligned time, distracted time, context switch counts</li>
        </ul>

        <h3>2.3 Device Information</h3>
        <p>
          When you connect a device (desktop app or browser extension), we store a device record
          including: device type, a device fingerprint, device name, software version, and last-seen
          timestamp. This is used for cross-device sync and presence tracking.
        </p>

        <h3>2.4 Payment Data</h3>
        <p>
          Payment information (credit card, billing address) is collected and processed entirely by{" "}
          <strong>Paddle.com</strong>, our Merchant of Record. We do not store or have access to your
          payment details. Paddle shares with us: subscription status, plan type, and transaction IDs.
        </p>

        <h3>2.5 Local-Only Data (Free/BYOK Users)</h3>
        <p>
          If you use Foqus without a cloud account, all data stays on your device. The desktop app
          stores data in a local SQLite database. The browser extension stores data in Chrome/Edge
          local storage. No data is sent to our servers.
        </p>

        <h2>3. How We Use Your Data</h2>
        <ul>
          <li>To provide the core Service: classify your current context against your active task</li>
          <li>To generate focus analytics and session history</li>
          <li>To sync sessions and settings across your devices</li>
          <li>To manage your subscription and account</li>
          <li>To send transactional emails (login links, subscription changes)</li>
          <li>To improve the Service (aggregated, anonymized usage patterns)</li>
        </ul>
        <p>
          We <strong>do not</strong> sell, rent, or share your personal data with advertisers. We{" "}
          <strong>do not</strong> use your data for purposes unrelated to the Service. We{" "}
          <strong>do not</strong> use your data to determine creditworthiness or for lending purposes.
        </p>

        <h2>4. Legal Basis (GDPR)</h2>
        <p>We process your data under the following legal bases:</p>
        <ul>
          <li>
            <strong>Contract performance</strong> — Processing necessary to deliver the Service you
            signed up for (account, sessions, sync).
          </li>
          <li>
            <strong>Legitimate interest</strong> — Aggregated analytics to improve the Service,
            security monitoring, fraud prevention.
          </li>
          <li>
            <strong>Consent</strong> — Marketing communications (if any, opt-in only).
          </li>
        </ul>

        <h2>5. Third-Party Processors</h2>
        <p>We share data with the following third-party services, only as needed to operate the Service:</p>
        <ul>
          <li>
            <strong>Supabase</strong> (auth provider) — Stores your email for authentication.
            See{" "}
            <a href="https://supabase.com/privacy" target="_blank" rel="noopener noreferrer">
              Supabase Privacy Policy
            </a>.
          </li>
          <li>
            <strong>AI classification providers</strong> (OpenAI, Anthropic, Google) — Receives
            window/page context and task description for classification. In BYOK mode, requests
            go directly from your device using your API key. In managed mode, requests route
            through our API. See each provider's privacy policy.
          </li>
          <li>
            <strong>Paddle</strong> (Merchant of Record) — Processes payments and manages
            subscriptions. See{" "}
            <a href="https://www.paddle.com/legal/privacy" target="_blank" rel="noopener noreferrer">
              Paddle Privacy Policy
            </a>.
          </li>
          <li>
            <strong>Hosting infrastructure</strong> — Our web API and database are hosted on
            cloud infrastructure within the EU.
          </li>
        </ul>

        <h2>6. Data Retention</h2>
        <ul>
          <li>
            <strong>Account data</strong> — Retained while your account is active. Deleted within
            30 days of account deletion request.
          </li>
          <li>
            <strong>Session and classification data</strong> — Retained while your account is active.
            Deleted within 30 days of account deletion.
          </li>
          <li>
            <strong>Server-side classification cache</strong> — Cached classification results expire
            after 24 hours.
          </li>
          <li>
            <strong>Local data</strong> — Remains on your device until you clear it or uninstall the
            app/extension.
          </li>
        </ul>

        <h2>7. Your Rights (GDPR)</h2>
        <p>Under the General Data Protection Regulation, you have the right to:</p>
        <ul>
          <li><strong>Access</strong> — Request a copy of the personal data we hold about you.</li>
          <li><strong>Rectification</strong> — Request correction of inaccurate data.</li>
          <li><strong>Erasure</strong> — Request deletion of your personal data ("right to be forgotten").</li>
          <li><strong>Portability</strong> — Request your data in a machine-readable format.</li>
          <li><strong>Restriction</strong> — Request that we limit processing of your data.</li>
          <li><strong>Objection</strong> — Object to processing based on legitimate interest.</li>
        </ul>
        <p>
          To exercise any of these rights, email us at{" "}
          <a href="mailto:bogdan@neuroqode.ai">bogdan@neuroqode.ai</a>. We will respond within 30 days.
        </p>

        <h2>8. Cookies and Local Storage</h2>
        <p>
          The Foqus website and web app use essential cookies and local storage for authentication
          (session tokens). We do not use advertising or tracking cookies. No third-party analytics
          or tracking scripts are loaded.
        </p>

        <h2>9. Data Security</h2>
        <p>
          We implement appropriate technical and organizational measures to protect your data,
          including encryption in transit (TLS), secure authentication (passwordless magic links),
          and access controls on our infrastructure.
        </p>

        <h2>10. Children's Privacy</h2>
        <p>
          The Service is not directed at children under 16. We do not knowingly collect personal data
          from children. If you believe a child has provided us with personal data, please contact us
          and we will delete it promptly.
        </p>

        <h2>11. Changes to This Policy</h2>
        <p>
          We may update this Privacy Policy from time to time. If we make material changes, we will
          notify you by email or through the Service. The "Last updated" date at the top will be revised.
        </p>

        <h2>12. Contact</h2>
        <div className="legal-contact-card">
          <p><strong>SC NeuroQode Solutions SRL</strong></p>
          <p>Data Protection Contact: <a href="mailto:bogdan@neuroqode.ai">bogdan@neuroqode.ai</a></p>
          <p>Country: Romania, European Union</p>
        </div>
      </div>
    </div>
  );
}

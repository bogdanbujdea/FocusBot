import { Link } from "react-router-dom";
import "./LegalPage.css";

export function TermsPage() {
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
        <h1>Terms &amp; Conditions</h1>
        <p className="legal-last-updated">Last updated: March 21, 2026</p>

        <h2>1. Introduction</h2>
        <p>
          These Terms and Conditions ("Terms") govern your use of the Foqus productivity platform
          ("Service"), operated by SC NeuroQode Solutions SRL ("Company", "we", "us"), a company
          registered in Romania. By accessing or using the Service, you agree to be bound by these Terms.
        </p>

        <h2>2. Service Description</h2>
        <p>
          Foqus is a productivity platform that classifies your current focus context against a single
          active task and tracks alignment over time. The Service consists of:
        </p>
        <ul>
          <li>A Windows desktop application for foreground window monitoring and focus scoring</li>
          <li>A browser extension (Chrome/Edge) for page/tab classification</li>
          <li>A web API for classification orchestration, sessions, and account management</li>
          <li>A web dashboard at app.foqus.me for analytics and account settings</li>
        </ul>

        <h2>3. Account Registration</h2>
        <p>
          To use cloud features of the Service, you must create an account using a valid email address.
          You are responsible for maintaining the confidentiality of your account and for all activities
          that occur under it. You must notify us immediately of any unauthorized use.
        </p>

        <h2>4. Subscription Tiers</h2>
        <p>The Service offers the following tiers:</p>
        <ul>
          <li>
            <strong>Free (BYOK)</strong> — Use your own API key for AI classification. Basic local
            analytics. No cloud sync.
          </li>
          <li>
            <strong>Foqus BYOK</strong> — Use your own API key with full cloud analytics, cross-device
            sync, and web dashboard access. Requires a paid subscription.
          </li>
          <li>
            <strong>Foqus Premium</strong> — Platform-managed AI classification key with full cloud
            analytics, cross-device sync, and web dashboard access. Requires a paid subscription.
          </li>
        </ul>

        <h2>5. Payments and Billing</h2>
        <p>
          Paid subscriptions are billed through <strong>Paddle.com</strong>, our Merchant of Record.
          Paddle handles all payment processing, invoicing, sales tax, and VAT on our behalf. By
          subscribing, you also agree to{" "}
          <a href="https://www.paddle.com/legal/terms" target="_blank" rel="noopener noreferrer">
            Paddle's Terms of Service
          </a>
          . Subscription fees are billed in advance on a recurring basis (monthly or annually, depending
          on your chosen plan).
        </p>

        <h2>6. Acceptable Use</h2>
        <p>You agree not to:</p>
        <ul>
          <li>Use the Service for any unlawful purpose</li>
          <li>Attempt to gain unauthorized access to the Service or its systems</li>
          <li>Reverse engineer, decompile, or disassemble any part of the Service</li>
          <li>Resell, redistribute, or sublicense access to the Service</li>
          <li>Interfere with or disrupt the integrity or performance of the Service</li>
          <li>Use the Service to process data on behalf of third parties without authorization</li>
        </ul>

        <h2>7. Intellectual Property</h2>
        <p>
          The Service, including its design, code, and branding, is the intellectual property of SC
          NeuroQode Solutions SRL. Your data remains yours — we claim no ownership over the content
          you create or the data generated through your use of the Service.
        </p>

        <h2>8. Third-Party Services</h2>
        <p>
          The Service integrates with third-party AI providers (such as OpenAI, Anthropic, and Google)
          for classification. When using BYOK mode, you provide your own API key and are subject to
          the respective provider's terms and privacy policy. In managed mode, the Company provides
          the API key, but classification data is still processed by the third-party provider.
        </p>

        <h2>9. Limitation of Liability</h2>
        <p>
          To the maximum extent permitted by applicable law, the Company shall not be liable for any
          indirect, incidental, special, consequential, or punitive damages, including loss of profits,
          data, or use, arising from your use of the Service. The Service is provided "as is" without
          warranties of any kind, express or implied.
        </p>

        <h2>10. Termination</h2>
        <p>
          You may terminate your account at any time by contacting us or through the account settings.
          We may suspend or terminate your access if you violate these Terms. Upon termination, your
          right to use the Service ceases immediately. Data deletion follows the timeline described
          in our Privacy Policy.
        </p>

        <h2>11. Changes to Terms</h2>
        <p>
          We may update these Terms from time to time. If we make material changes, we will notify
          you by email or through the Service. Continued use after changes constitutes acceptance of
          the revised Terms.
        </p>

        <h2>12. Governing Law</h2>
        <p>
          These Terms are governed by the laws of Romania and the European Union. Any disputes arising
          from these Terms shall be subject to the exclusive jurisdiction of the courts of Romania.
        </p>

        <h2>13. Contact</h2>
        <div className="legal-contact-card">
          <p><strong>SC NeuroQode Solutions SRL</strong></p>
          <p>Email: <a href="mailto:bogdan@neuroqode.ai">bogdan@neuroqode.ai</a></p>
          <p>Country: Romania, European Union</p>
        </div>
      </div>
    </div>
  );
}

import { Link } from "react-router-dom";
import "./LegalPage.css";

export function RefundPage() {
  return (
    <div className="legal-page">
      <header className="legal-page-header">
        <div className="legal-page-header-inner">
          <Link to="/" className="legal-back-link">
            &larr; Back to Dashboard
          </Link>
        </div>
      </header>

      <div className="legal-page-content">
        <h1>Refund Policy</h1>
        <p className="legal-last-updated">Last updated: March 21, 2026</p>

        <h2>1. 30-Day Money-Back Guarantee</h2>
        <p>
          We offer a <strong>30-day money-back guarantee</strong> on all paid Foqus subscriptions.
          If you are not satisfied with the Service for any reason, you can request a full refund
          within 30 days of your initial subscription purchase — no questions asked.
        </p>

        <h2>2. How to Request a Refund</h2>
        <p>
          To request a refund, email us at{" "}
          <a href="mailto:bogdan@neuroqode.ai">bogdan@neuroqode.ai</a> with the email address
          associated with your Foqus account. We will process your refund within 5-10 business days.
        </p>

        <h2>3. Refund Processing</h2>
        <p>
          All payments are processed by <strong>Paddle.com</strong>, our Merchant of Record.
          Refunds are issued through Paddle to your original payment method. The refund will appear
          on your statement within 5-10 business days, depending on your payment provider.
        </p>

        <h2>4. After the 30-Day Period</h2>
        <p>
          After the initial 30-day period, subscriptions are non-refundable. However, you can{" "}
          <strong>cancel your subscription at any time</strong>, and you will retain access to paid
          features until the end of your current billing period. No further charges will be made
          after cancellation.
        </p>

        <h2>5. Subscription Renewals</h2>
        <p>
          The 30-day money-back guarantee applies only to the <strong>first payment</strong> of a new
          subscription. Renewal payments are not eligible for the money-back guarantee but can still
          be cancelled to prevent future charges.
        </p>

        <h2>6. Exceptional Circumstances</h2>
        <p>
          If you experience technical issues that prevent you from using the Service, or if you
          believe you were charged in error, please contact us regardless of the 30-day window.
          We will review your case and work to find a fair resolution.
        </p>

        <h2>7. Paddle Buyer Protection</h2>
        <p>
          As Paddle is our Merchant of Record, you are also covered by{" "}
          <a href="https://www.paddle.com/legal/buyer-terms" target="_blank" rel="noopener noreferrer">
            Paddle's Buyer Terms
          </a>{" "}
          which provide additional buyer protection. You may also contact Paddle directly for
          payment-related disputes.
        </p>

        <h2>8. Contact</h2>
        <div className="legal-contact-card">
          <p><strong>SC NeuroQode Solutions SRL</strong></p>
          <p>Email: <a href="mailto:bogdan@neuroqode.ai">bogdan@neuroqode.ai</a></p>
          <p>Country: Romania, European Union</p>
        </div>
      </div>
    </div>
  );
}

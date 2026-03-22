import { useEffect, useState } from "react";
import { api } from "../api/client";
import type { SubscriptionStatusResponse } from "../api/types";
import { getPlanDisplayName } from "../api/types";
import "./BillingPage.css";

export function BillingPage() {
  const [subscription, setSubscription] =
    useState<SubscriptionStatusResponse | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    async function load() {
      const status = await api.getSubscriptionStatus();
      setSubscription(status);
      setLoading(false);
    }
    load();
  }, []);

  if (loading) {
    return (
      <div className="billing-page">
        <header className="page-header">
          <h1 className="page-title">Billing</h1>
          <p className="page-subtitle">Loading subscription info...</p>
        </header>
      </div>
    );
  }

  const status = subscription?.status ?? "none";
  const planName = getPlanDisplayName(subscription?.planType ?? 0);
  const isActive = status === "active" || status === "trial";

  return (
    <div className="billing-page">
      <header className="page-header">
        <h1 className="page-title">Billing</h1>
        <p className="page-subtitle">Manage your subscription</p>
      </header>

      <section className="billing-section">
        <div className="billing-card">
          <div className="plan-header">
            <div className="plan-info">
              <h2 className="plan-name">{planName}</h2>
              <span className={`plan-badge ${isActive ? "active" : "inactive"}`}>
                {status === "trial"
                  ? "Trial"
                  : status === "active"
                    ? "Active"
                    : status === "canceled"
                      ? "Canceled"
                      : "Free"}
              </span>
            </div>
          </div>

          {status === "trial" && subscription?.trialEndsAt && (
            <p className="plan-detail">
              Trial ends:{" "}
              {new Date(subscription.trialEndsAt).toLocaleString("en-US", {
                dateStyle: "medium",
                timeStyle: "short",
              })}
            </p>
          )}

          {status === "active" && subscription?.currentPeriodEndsAt && (
            <p className="plan-detail">
              Current period ends:{" "}
              {new Date(subscription.currentPeriodEndsAt).toLocaleString(
                "en-US",
                {
                  dateStyle: "medium",
                  timeStyle: "short",
                }
              )}
            </p>
          )}
        </div>
      </section>

      <section className="billing-section">
        <h2 className="section-title">Plans</h2>
        <div className="plans-grid">
          <PlanCard
            name="Free (BYOK)"
            price="$0"
            features={[
              "Bring your own API key",
              "Local analytics in desktop app",
              "Browser extension support",
            ]}
            current={subscription?.planType === 0}
          />
          <PlanCard
            name="Cloud BYOK"
            price="$4.99/mo"
            features={[
              "Everything in Free",
              "Cloud session sync",
              "Full web analytics dashboard",
              "Cross-device insights",
            ]}
            current={subscription?.planType === 1}
            highlighted
          />
          <PlanCard
            name="Cloud Managed"
            price="$9.99/mo"
            features={[
              "Everything in Cloud BYOK",
              "No API key needed",
              "Platform-managed AI access",
              "Priority support",
            ]}
            current={subscription?.planType === 2}
          />
        </div>
      </section>
    </div>
  );
}

function PlanCard({
  name,
  price,
  features,
  current,
  highlighted,
}: {
  name: string;
  price: string;
  features: string[];
  current?: boolean;
  highlighted?: boolean;
}) {
  return (
    <div
      className={`plan-card ${highlighted ? "highlighted" : ""} ${current ? "current" : ""}`}
    >
      <h3 className="plan-card-name">{name}</h3>
      <div className="plan-card-price">{price}</div>
      <ul className="plan-features">
        {features.map((f) => (
          <li key={f}>{f}</li>
        ))}
      </ul>
      {current ? (
        <div className="current-plan-label">Current plan</div>
      ) : (
        <button className="plan-upgrade-btn" disabled>
          Coming soon
        </button>
      )}
    </div>
  );
}

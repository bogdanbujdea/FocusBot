import { useCallback, useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { api } from "../api/client";
import type { PricingPlanDto, SubscriptionStatusResponse } from "../api/types";
import { getPlanDisplayName, PlanType } from "../api/types";
import { useAuth } from "../auth/useAuth";
import { useSubscription } from "../contexts/SubscriptionContext";
import { usePaddle } from "../hooks/usePaddle";
import "./BillingPage.css";

function formatMinorAmount(minor: number, currency: string): string {
  const major = minor / 100;
  try {
    return new Intl.NumberFormat(undefined, {
      style: "currency",
      currency: currency || "USD",
    }).format(major);
  } catch {
    return `${major.toFixed(2)} ${currency}`;
  }
}

function planSlugMatchesCurrentPlan(
  slug: string,
  subscription: SubscriptionStatusResponse | null
): boolean {
  if (!subscription) return false;
  if (slug === "cloud-byok" && subscription.planType === PlanType.CloudBYOK) return true;
  if (slug === "cloud-managed" && subscription.planType === PlanType.CloudManaged)
    return true;
  return false;
}

function statusBadgeLabel(status: string): string {
  switch (status) {
    case "trial":
      return "Trial";
    case "active":
      return "Active";
    case "canceled":
      return "Canceled";
    case "expired":
      return "Expired";
    default:
      return "No plan";
  }
}

export function BillingPage() {
  const { user } = useAuth();
  const { subscription: contextSubscription, refresh: refreshContext } = useSubscription();
  const [searchParams, setSearchParams] = useSearchParams();
  const [localSubscription, setLocalSubscription] =
    useState<SubscriptionStatusResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [checkoutBanner, setCheckoutBanner] = useState(false);
  const [portalError, setPortalError] = useState<string | null>(null);

  // Use context subscription if available; fall back to local fetch during initial load.
  const subscription = localSubscription ?? contextSubscription;

  const reloadSubscription = useCallback(async () => {
    const status = await api.getSubscriptionStatus();
    setLocalSubscription(status);
    void refreshContext();
  }, [refreshContext]);

  const onCheckoutDone = useCallback(() => {
    setCheckoutBanner(true);
    void reloadSubscription();
  }, [reloadSubscription]);

  const { pricing, loadError, ready, openCheckout } = usePaddle(onCheckoutDone);

  useEffect(() => {
    void (async () => {
      await reloadSubscription();
      setLoading(false);
    })();
  }, [reloadSubscription]);

  useEffect(() => {
    if (searchParams.get("checkout") === "success") {
      setCheckoutBanner(true);
      void reloadSubscription();
      searchParams.delete("checkout");
      setSearchParams(searchParams, { replace: true });
    }
  }, [searchParams, setSearchParams, reloadSubscription]);

  const sortedPaidPlans = useMemo(() => {
    if (!pricing?.plans?.length) return [];
    return [...pricing.plans].sort((a, b) => {
      if (a.unitAmountMinor !== b.unitAmountMinor)
        return a.unitAmountMinor - b.unitAmountMinor;
      return a.planType.localeCompare(b.planType);
    });
  }, [pricing?.plans]);

  const handleSubscribe = (plan: PricingPlanDto) => {
    if (!user?.id) return;
    const email =
      typeof user.email === "string" ? user.email : user.email ?? undefined;
    openCheckout(plan.priceId, plan.planType, email, user.id);
  };

  const handleManageSubscription = async () => {
    setPortalError(null);
    const result = await api.createCustomerPortalSession();
    if (!result.ok) {
      setPortalError(result.error ?? "Could not open customer portal.");
      return;
    }
    window.open(result.data.url, "_blank", "noopener,noreferrer");
  };

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
  const isActive = status === "active";
  const isTrial = status === "trial";
  const planName = isActive
    ? getPlanDisplayName(subscription?.planType ?? 0)
    : isTrial
      ? "Trial — Full Access"
      : getPlanDisplayName(subscription?.planType ?? 0);

  const canPortal = isActive;

  return (
    <div className="billing-page">
      <header className="page-header">
        <h1 className="page-title">Billing</h1>
        <p className="page-subtitle">Manage your subscription</p>
      </header>

      {checkoutBanner ? (
        <div className="billing-banner" role="status">
          Checkout completed. If your plan does not update immediately, refresh in a
          moment while we sync with Paddle.
        </div>
      ) : null}

      {loadError ? (
        <div className="billing-banner error" role="alert">
          {loadError}
        </div>
      ) : null}

      {portalError ? (
        <div className="billing-banner error" role="alert">
          {portalError}
        </div>
      ) : null}

      <section className="billing-section">
        <div className="billing-card">
          <div className="plan-header">
            <div className="plan-info">
              <h2 className="plan-name">{planName}</h2>
              <span
                className={`plan-badge ${isActive || isTrial ? "active" : "inactive"}`}
              >
                {statusBadgeLabel(status)}
              </span>
            </div>
          </div>

          {isTrial && subscription?.trialEndsAt ? (
            <p className="plan-detail">
              Trial ends:{" "}
              {new Date(subscription.trialEndsAt).toLocaleString("en-US", {
                dateStyle: "medium",
                timeStyle: "short",
              })}
            </p>
          ) : null}

          {isTrial ? (
            <p className="plan-detail plan-detail-hint">
              Choose a plan below before your trial ends to keep your data synced.
            </p>
          ) : null}

          {isActive && subscription?.currentPeriodEndsAt ? (
            <p className="plan-detail">
              Current period ends:{" "}
              {new Date(subscription.currentPeriodEndsAt).toLocaleString("en-US", {
                dateStyle: "medium",
                timeStyle: "short",
              })}
            </p>
          ) : null}

          {subscription?.nextBilledAtUtc ? (
            <p className="plan-detail">
              Next bill:{" "}
              {new Date(subscription.nextBilledAtUtc).toLocaleString("en-US", {
                dateStyle: "medium",
                timeStyle: "short",
              })}
            </p>
          ) : null}

          {canPortal ? (
            <button
              type="button"
              className="plan-manage-btn"
              onClick={() => void handleManageSubscription()}
            >
              Manage subscription
            </button>
          ) : null}
        </div>
      </section>

      <section className="billing-section">
        <h2 className="section-title">Plans</h2>
        <div className="plans-grid">
          {sortedPaidPlans.map((plan) => (
            <PlanCard
              key={plan.priceId}
              name={plan.name}
              price={`${formatMinorAmount(plan.unitAmountMinor, plan.currency)}${
                plan.billingInterval ? ` / ${plan.billingInterval}` : ""
              }`}
              features={[
                plan.description ?? "Cloud sync, analytics, and dashboard on foqus.me",
                plan.planType === "cloud-byok"
                  ? "You provide your OpenAI (or other) API key"
                  : "Platform-managed AI — no API key needed",
              ]}
              current={planSlugMatchesCurrentPlan(plan.planType, subscription)}
              highlighted={plan.planType === "cloud-byok"}
              actionLabel={
                ready && user
                  ? planSlugMatchesCurrentPlan(plan.planType, subscription)
                    ? undefined
                    : "Subscribe"
                  : undefined
              }
              onAction={
                ready && user
                  ? () => handleSubscribe(plan)
                  : undefined
              }
              disabled={!ready || !user}
            />
          ))}
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
  actionLabel,
  onAction,
  disabled,
}: {
  name: string;
  price: string;
  features: string[];
  current?: boolean;
  highlighted?: boolean;
  actionLabel?: string;
  onAction?: () => void;
  disabled?: boolean;
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
      ) : actionLabel && onAction ? (
        <button
          type="button"
          className="plan-upgrade-btn"
          disabled={disabled}
          onClick={onAction}
        >
          {actionLabel}
        </button>
      ) : null}
    </div>
  );
}

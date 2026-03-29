import { useCallback, useEffect, useMemo, useState } from "react";
import { Outlet, NavLink, Link, useLocation } from "react-router-dom";
import { useAuth } from "../auth/useAuth";
import { SubscriptionProvider, useSubscription } from "../contexts/SubscriptionContext";
import { TrialWelcomeModal, trialWelcomeSeenKey } from "./TrialWelcomeModal";
import "./Layout.css";

function TrialBanner() {
  const { subscription } = useSubscription();

  const timeLeft = useMemo(() => {
    if (!subscription?.trialEndsAt) return null;
    const ms = new Date(subscription.trialEndsAt).getTime() - Date.now();
    if (ms <= 0) return null;
    const hours = Math.floor(ms / 3_600_000);
    const minutes = Math.floor((ms % 3_600_000) / 60_000);
    return hours > 0 ? `${hours}h ${minutes}m remaining` : `${minutes}m remaining`;
  }, [subscription?.trialEndsAt]);

  if (subscription?.status !== "trial" || !timeLeft) return null;

  return (
    <div className="trial-banner" role="status">
      <span className="trial-banner-text">
        Trial active &mdash; <strong>{timeLeft}</strong>
      </span>
      <Link to="/billing" className="trial-banner-link">
        Choose a plan
      </Link>
    </div>
  );
}

function LayoutContent() {
  const { user, signOut } = useAuth();
  const { subscription } = useSubscription();
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [modalDismissed, setModalDismissed] = useState(true);
  const location = useLocation();

  const closeSidebar = useCallback(() => setSidebarOpen(false), []);

  useEffect(() => {
    closeSidebar();
  }, [location.pathname, closeSidebar]);

  useEffect(() => {
    if (!user?.id || subscription?.status !== "trial") return;
    const seen = localStorage.getItem(trialWelcomeSeenKey(user.id));
    setModalDismissed(seen === "true");
  }, [user?.id, subscription?.status]);

  const handleModalDismiss = useCallback(() => {
    if (user?.id) {
      localStorage.setItem(trialWelcomeSeenKey(user.id), "true");
    }
    setModalDismissed(true);
  }, [user?.id]);

  const showModal =
    !modalDismissed &&
    subscription?.status === "trial" &&
    !!subscription.trialEndsAt;

  return (
    <div className="layout">
      <TrialBanner />
      {showModal && (
        <TrialWelcomeModal
          trialEndsAt={subscription.trialEndsAt!}
          onDismiss={handleModalDismiss}
        />
      )}
      <div className="layout-body">
        {sidebarOpen && (
          <div className="sidebar-overlay" onClick={closeSidebar} />
        )}
        <button
          type="button"
          className="mobile-menu-btn"
          aria-label="Open navigation"
          onClick={() => setSidebarOpen((o) => !o)}
        >
          <span className="hamburger-icon" />
        </button>
        <aside className={`sidebar${sidebarOpen ? " sidebar-open" : ""}`}>
          <div className="sidebar-header">
            <h1 className="logo">Foqus</h1>
          </div>
          <nav className="sidebar-nav">
            <NavLink to="/" end className="nav-link">
              <span className="nav-icon">📊</span>
              Dashboard
            </NavLink>
            <NavLink to="/analytics" className="nav-link">
              <span className="nav-icon">📈</span>
              Analytics
            </NavLink>
            <NavLink to="/integrations" className="nav-link">
              <span className="nav-icon">🔗</span>
              Integrations
            </NavLink>
            <NavLink to="/settings" className="nav-link">
              <span className="nav-icon">⚙️</span>
              Settings
            </NavLink>
            <NavLink to="/billing" className="nav-link">
              <span className="nav-icon">💳</span>
              Billing
            </NavLink>
          </nav>
          <div className="sidebar-footer">
            <div className="user-info">
              <span className="user-email">{user?.email}</span>
            </div>
            <button onClick={signOut} className="sign-out-button">
              Sign out
            </button>
            <nav className="sidebar-legal-links" aria-label="Legal">
              <Link to="/terms">Terms</Link>
              <Link to="/privacy">Privacy</Link>
              <Link to="/refund">Refunds</Link>
            </nav>
          </div>
        </aside>
        <main className="main-content">
          <Outlet />
        </main>
      </div>
    </div>
  );
}

export function Layout() {
  return (
    <SubscriptionProvider>
      <LayoutContent />
    </SubscriptionProvider>
  );
}

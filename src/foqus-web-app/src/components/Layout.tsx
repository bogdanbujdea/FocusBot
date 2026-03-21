import { Outlet, NavLink, Link } from "react-router-dom";
import { useAuth } from "../auth/useAuth";
import "./Layout.css";

export function Layout() {
  const { user, signOut } = useAuth();

  return (
    <div className="layout">
      <aside className="sidebar">
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
  );
}

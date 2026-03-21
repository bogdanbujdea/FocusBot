import "./DashboardPage.css";

export function DashboardPage() {
  return (
    <div className="dashboard-page">
      <header className="page-header">
        <h1 className="page-title">Dashboard</h1>
        <p className="page-subtitle">
          Your focus overview across all devices
        </p>
      </header>

      <div className="dashboard-grid">
        <div className="stat-card">
          <div className="stat-label">Today's Focus</div>
          <div className="stat-value">--</div>
          <div className="stat-hint">Connect a device to start tracking</div>
        </div>

        <div className="stat-card">
          <div className="stat-label">Sessions Today</div>
          <div className="stat-value">0</div>
        </div>

        <div className="stat-card">
          <div className="stat-label">Focus Time</div>
          <div className="stat-value">0h 0m</div>
        </div>

        <div className="stat-card">
          <div className="stat-label">Distraction Time</div>
          <div className="stat-value">0h 0m</div>
        </div>
      </div>

      <section className="recent-sessions">
        <h2 className="section-title">Recent Sessions</h2>
        <div className="empty-state">
          <p>No sessions yet. Start a focus session in the desktop app or browser extension.</p>
        </div>
      </section>
    </div>
  );
}

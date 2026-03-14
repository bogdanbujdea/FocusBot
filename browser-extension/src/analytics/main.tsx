import { useEffect, useState } from "react";
import { createRoot } from "react-dom/client";
import { sendRuntimeRequest } from "../shared/runtime";
import type { AnalyticsResponse, DateRange, DomainAggregate } from "../shared/types";
import { formatSeconds } from "../shared/utils";
import "../ui/styles.css";

const DomainList = ({ title, domains }: { title: string; domains: DomainAggregate[] }): JSX.Element => (
  <section className="card">
    <h3>{title}</h3>
    {domains.length === 0 ? (
      <p className="muted">No data yet.</p>
    ) : (
      <ul className="domain-list">
        {domains.map((domain) => (
          <li key={domain.domain}>
            <span>{domain.domain}</span>
            <span>{formatSeconds(domain.totalSeconds)}</span>
          </li>
        ))}
      </ul>
    )}
  </section>
);

const AnalyticsPage = (): JSX.Element => {
  const [range, setRange] = useState<DateRange>("today");
  const [report, setReport] = useState<AnalyticsResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    void (async () => {
      setLoading(true);
      setError("");
      const response = await sendRuntimeRequest<AnalyticsResponse>({ type: "GET_ANALYTICS", range });
      if (!response.ok || !response.data) {
        setError(response.error ?? "Unable to load analytics.");
        setLoading(false);
        return;
      }
      setReport(response.data);
      setLoading(false);
    })();
  }, [range]);

  return (
    <main className="app-shell">
      <header>
        <h1>Deep Work Analytics</h1>
        <p className="muted">Daily focus quality for today, last 7 days, or last 30 days.</p>
      </header>

      <section className="card">
        <h2>Date Range</h2>
        <select value={range} onChange={(event) => setRange(event.target.value as DateRange)}>
          <option value="today">Today</option>
          <option value="7d">Last 7 days</option>
          <option value="30d">Last 30 days</option>
        </select>
      </section>

      {loading ? <p className="muted">Loading analytics...</p> : null}
      {error ? <p className="error">{error}</p> : null}

      {report ? (
        <>
          <section className="card">
            <h2>Summary Totals</h2>
            <div className="grid two">
              <p>
                <strong>Sessions:</strong> {report.totals.totalSessions}
              </p>
              <p>
                <strong>Focus %:</strong> {report.totals.focusPercentage.toFixed(1)}%
              </p>
              <p>
                <strong>Tracked:</strong> {formatSeconds(report.totals.totalTrackedSeconds)}
              </p>
              <p>
                <strong>Aligned:</strong> {formatSeconds(report.totals.totalAlignedSeconds)}
              </p>
              <p>
                <strong>Distracting:</strong> {formatSeconds(report.totals.totalDistractingSeconds)}
              </p>
              <p>
                <strong>Avg context switch:</strong> {formatSeconds(report.totals.averageContextSwitchCostSeconds)}
              </p>
            </div>
          </section>

          <section className="card">
            <h2>Daily Breakdown</h2>
            <table className="analytics-table">
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Sessions</th>
                  <th>Tracked</th>
                  <th>Focus %</th>
                  <th>Distractions</th>
                </tr>
              </thead>
              <tbody>
                {report.statsByDay.map((row) => (
                  <tr key={row.date}>
                    <td>{row.date}</td>
                    <td>{row.totalSessions}</td>
                    <td>{formatSeconds(row.totalTrackedSeconds)}</td>
                    <td>{row.focusPercentage.toFixed(1)}%</td>
                    <td>{row.distractionCount}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </section>

          <DomainList title="Top distracting domains" domains={report.totals.mostCommonDistractingDomains} />
          <DomainList title="Top aligned domains" domains={report.totals.mostCommonAlignedDomains} />
        </>
      ) : null}
    </main>
  );
};

const container = document.getElementById("root");
if (!container) {
  throw new Error("Root container not found.");
}

createRoot(container).render(<AnalyticsPage />);

import { useEffect, useState } from "react";
import { createRoot } from "react-dom/client";
import {
  Chart as ChartJS,
  CategoryScale,
  LinearScale,
  BarElement,
  Title,
  Tooltip,
  Legend
} from "chart.js";
import { Bar } from "react-chartjs-2";
import { sendRuntimeRequest } from "../shared/runtime";
import type { AnalyticsResponse, DateRange, DailyStats } from "../shared/types";
import { formatSeconds } from "../shared/utils";
import "../ui/styles.css";

ChartJS.register(CategoryScale, LinearScale, BarElement, Title, Tooltip, Legend);

const FocusGauge = ({ percentage }: { percentage: number }): JSX.Element => {
  const radius = 42;
  const circumference = 2 * Math.PI * radius;
  const strokeDashoffset = circumference - (percentage / 100) * circumference;

  return (
    <div className="focus-gauge">
      <svg viewBox="0 0 100 100">
        <circle className="focus-gauge-bg" cx="50" cy="50" r={radius} />
        <circle
          className="focus-gauge-fill"
          cx="50"
          cy="50"
          r={radius}
          strokeDasharray={circumference}
          strokeDashoffset={strokeDashoffset}
        />
      </svg>
      <div className="focus-gauge-text">
        <div className="focus-gauge-value">{percentage.toFixed(0)}%</div>
        <div className="focus-gauge-label">Focus</div>
      </div>
    </div>
  );
};

const KpiCard = ({
  label,
  value,
  sublabel,
  variant
}: {
  label: string;
  value: string;
  sublabel?: string;
  variant?: "aligned" | "distracting";
}): JSX.Element => (
  <div className={`kpi-card ${variant ?? ""}`}>
    <span className="kpi-label">{label}</span>
    <span className="kpi-value">{value}</span>
    {sublabel ? <span className="kpi-sublabel">{sublabel}</span> : null}
  </div>
);

const FocusScoreCard = ({
  percentage,
  alignedTime,
  distractingTime
}: {
  percentage: number;
  alignedTime: string;
  distractingTime: string;
}): JSX.Element => (
  <div className="kpi-card focus-score">
    <div className="focus-score-details">
      <span className="kpi-label">Focus Score</span>
      <div style={{ display: "flex", gap: "16px", marginTop: "4px" }}>
        <div>
          <span className="kpi-sublabel">Aligned</span>
          <div style={{ color: "#2db871", fontWeight: 600 }}>{alignedTime}</div>
        </div>
        <div>
          <span className="kpi-sublabel">Distracting</span>
          <div style={{ color: "#ff6666", fontWeight: 600 }}>{distractingTime}</div>
        </div>
      </div>
    </div>
    <FocusGauge percentage={percentage} />
  </div>
);

const formatDayLabel = (dateStr: string): string => {
  const date = new Date(dateStr);
  const today = new Date();
  const yesterday = new Date(today);
  yesterday.setDate(yesterday.getDate() - 1);

  if (dateStr === today.toISOString().split("T")[0]) {
    return "Today";
  }
  if (dateStr === yesterday.toISOString().split("T")[0]) {
    return "Yesterday";
  }

  return date.toLocaleDateString("en-US", { weekday: "short", month: "short", day: "numeric" });
};

const DailyChart = ({ statsByDay }: { statsByDay: DailyStats[] }): JSX.Element => {
  const labels = statsByDay.map((day) => formatDayLabel(day.date));

  const data = {
    labels,
    datasets: [
      {
        label: "Aligned",
        data: statsByDay.map((day) => Math.round(day.totalAlignedSeconds / 60)),
        backgroundColor: "rgba(45, 184, 113, 0.8)",
        borderRadius: 4,
        borderSkipped: false
      },
      {
        label: "Distracting",
        data: statsByDay.map((day) => Math.round(day.totalDistractingSeconds / 60)),
        backgroundColor: "rgba(255, 102, 102, 0.8)",
        borderRadius: 4,
        borderSkipped: false
      }
    ]
  };

  const options = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        display: false
      },
      tooltip: {
        backgroundColor: "rgba(15, 18, 32, 0.95)",
        titleColor: "#e7ecff",
        bodyColor: "#e7ecff",
        borderColor: "rgba(255, 255, 255, 0.1)",
        borderWidth: 1,
        padding: 12,
        cornerRadius: 8,
        callbacks: {
          label: (context: { dataset: { label: string }; parsed: { y: number } }) => {
            const minutes = context.parsed.y;
            if (minutes >= 60) {
              const hours = Math.floor(minutes / 60);
              const mins = minutes % 60;
              return `${context.dataset.label}: ${hours}h ${mins}m`;
            }
            return `${context.dataset.label}: ${minutes}m`;
          }
        }
      }
    },
    scales: {
      x: {
        stacked: true,
        grid: {
          display: false
        },
        ticks: {
          color: "rgba(231, 236, 255, 0.5)",
          font: {
            size: 11
          }
        }
      },
      y: {
        stacked: true,
        grid: {
          color: "rgba(255, 255, 255, 0.06)"
        },
        ticks: {
          color: "rgba(231, 236, 255, 0.5)",
          font: {
            size: 11
          },
          callback: (value: number | string) => {
            const minutes = Number(value);
            if (minutes >= 60) {
              return `${Math.floor(minutes / 60)}h`;
            }
            return `${minutes}m`;
          }
        }
      }
    }
  };

  return (
    <div className="chart-card">
      <h3>Daily Activity</h3>
      <div className="chart-container tall">
        <Bar data={data} options={options} />
      </div>
      <div className="chart-legend">
        <div className="legend-item">
          <span className="legend-dot aligned" />
          Aligned
        </div>
        <div className="legend-item">
          <span className="legend-dot distracting" />
          Distracting
        </div>
      </div>
    </div>
  );
};

const InsightCard = ({
  icon,
  label,
  value,
  iconClass
}: {
  icon: string;
  label: string;
  value: string;
  iconClass: string;
}): JSX.Element => (
  <div className="insight-card">
    <div className={`insight-icon ${iconClass}`}>{icon}</div>
    <div className="insight-content">
      <span className="insight-label">{label}</span>
      <span className="insight-value">{value}</span>
    </div>
  </div>
);

const formatBestDay = (dateStr: string): string => {
  const date = new Date(dateStr);
  return date.toLocaleDateString("en-US", { weekday: "long", month: "short", day: "numeric" });
};

const EmptyState = (): JSX.Element => (
  <div className="empty-state">
    <div className="empty-state-icon">~</div>
    <p className="empty-state-text">
      No focus sessions recorded yet.
      <br />
      Start a session to see your analytics!
    </p>
  </div>
);

const AnalyticsPage = (): JSX.Element => {
  const [range, setRange] = useState<DateRange>("today");
  const [report, setReport] = useState<AnalyticsResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [refreshKey, setRefreshKey] = useState(0);

  const fetchAnalytics = async () => {
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
  };

  useEffect(() => {
    void fetchAnalytics();
  }, [range, refreshKey]);

  const handleRefresh = () => {
    setRefreshKey((prev) => prev + 1);
  };

  const hasData = report && report.totals.totalSessions > 0;

  return (
    <main className="app-shell">
      <header className="analytics-header">
        <div className="analytics-title-row">
          <h1>Focus Insights</h1>
          <button
            className="refresh-button"
            onClick={handleRefresh}
            disabled={loading}
            title="Refresh analytics"
          >
            <svg
              className={loading ? "spinning" : ""}
              width="16"
              height="16"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            >
              <path d="M21 12a9 9 0 1 1-9-9c2.52 0 4.93 1 6.74 2.74L21 8" />
              <path d="M21 3v5h-5" />
            </svg>
          </button>
        </div>
        <p className="muted">Track your deep work patterns and stay aligned with your goals.</p>
      </header>

      <div className="segmented-control">
        {(["today", "7d", "30d"] as DateRange[]).map((r) => (
          <button key={r} className={range === r ? "active" : ""} onClick={() => setRange(r)}>
            {r === "today" ? "Today" : r === "7d" ? "7 Days" : "30 Days"}
          </button>
        ))}
      </div>

      {loading ? <p className="muted">Loading analytics...</p> : null}
      {error ? <p className="error">{error}</p> : null}

      {report && !loading ? (
        hasData ? (
          <>
            <div className="kpi-grid">
              <FocusScoreCard
                percentage={report.totals.focusPercentage}
                alignedTime={formatSeconds(report.totals.totalAlignedSeconds)}
                distractingTime={formatSeconds(report.totals.totalDistractingSeconds)}
              />
              <KpiCard
                label="Deep Work"
                value={formatSeconds(report.totals.totalAlignedSeconds)}
                sublabel="Time aligned with task"
                variant="aligned"
              />
              <KpiCard
                label="Sessions"
                value={report.totals.totalSessions.toString()}
                sublabel={`${formatSeconds(report.totals.totalTrackedSeconds)} tracked`}
              />
              <KpiCard
                label="Distractions"
                value={report.totals.distractionCount.toString()}
                sublabel={
                  report.totals.averageContextSwitchCostSeconds > 0
                    ? `Avg ${formatSeconds(report.totals.averageContextSwitchCostSeconds)} each`
                    : "None recorded"
                }
                variant="distracting"
              />
            </div>

            {range !== "today" && report.statsByDay.length > 1 ? (
              <DailyChart statsByDay={report.statsByDay} />
            ) : null}

            {report.insights.bestFocusDay || report.insights.averageSessionLengthSeconds > 0 ? (
              <div className="insights-row">
                {report.insights.bestFocusDay && range !== "today" ? (
                  <InsightCard
                    icon="*"
                    label="Best Focus Day"
                    value={`${formatBestDay(report.insights.bestFocusDay.date)} (${report.insights.bestFocusDay.focusPercentage.toFixed(0)}%)`}
                    iconClass="best-day"
                  />
                ) : null}
                {report.insights.averageSessionLengthSeconds > 0 ? (
                  <InsightCard
                    icon="@"
                    label="Avg Session Length"
                    value={formatSeconds(report.insights.averageSessionLengthSeconds)}
                    iconClass="session-length"
                  />
                ) : null}
              </div>
            ) : null}
          </>
        ) : (
          <EmptyState />
        )
      ) : null}
    </main>
  );
};

const container = document.getElementById("root");
if (!container) {
  throw new Error("Root container not found.");
}

createRoot(container).render(<AnalyticsPage />);

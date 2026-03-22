import type { ReactNode } from "react";
import { FocusGauge } from "./FocusGauge";
import "./shared.css";

export type KpiCardVariant =
  | "default"
  | "aligned"
  | "distracted"
  | "focus-score";

export interface KpiCardProps {
  label: string;
  value?: ReactNode;
  sublabel?: ReactNode;
  variant?: KpiCardVariant;
  /** When variant is focus-score, percentage for the donut (0–100). */
  focusPercentage?: number;
  /** Optional extra content inside the focus-score row (e.g. aligned/distracting times). */
  focusDetails?: ReactNode;
}

export function KpiCard({
  label,
  value,
  sublabel,
  variant = "default",
  focusPercentage,
  focusDetails,
}: KpiCardProps) {
  if (variant === "focus-score") {
    const pct =
      typeof focusPercentage === "number" && !Number.isNaN(focusPercentage)
        ? focusPercentage
        : 0;
    return (
      <div className="kpi-card kpi-card-focus-score" data-variant={variant}>
        <div className="kpi-card-focus-score-inner">
          <div className="kpi-card-focus-left">
            <div className="kpi-label">{label}</div>
            <div className="kpi-focus-details">{focusDetails}</div>
            {value !== undefined &&
              value !== null &&
              value !== "" && (
                <div className="kpi-value kpi-value-focus-primary">{value}</div>
              )}
            {sublabel !== undefined && sublabel !== null && (
              <div className="kpi-sublabel">{sublabel}</div>
            )}
          </div>
          <FocusGauge percentage={pct} size={100} />
        </div>
      </div>
    );
  }

  return (
    <div className="kpi-card" data-variant={variant}>
      <div className="kpi-label">{label}</div>
      {value !== undefined && value !== null && value !== "" && (
        <div className="kpi-value">{value}</div>
      )}
      {sublabel !== undefined && sublabel !== null && (
        <div className="kpi-sublabel">{sublabel}</div>
      )}
    </div>
  );
}

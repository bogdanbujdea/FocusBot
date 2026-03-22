import {
  FOCUS_GAUGE_RADIUS,
  focusGaugeCircumference,
  focusGaugeStrokeDashoffset,
} from "../utils/focusGaugeMath";
import "./shared.css";

export interface FocusGaugeProps {
  percentage: number;
  size?: number;
  label?: string;
}

/**
 * SVG ring progress; percentage clamped 0–100.
 * strokeDashoffset math matches browser extension analytics gauge.
 */
export function FocusGauge({
  percentage,
  size = 100,
  label = "Focus",
}: FocusGaugeProps) {
  const pct = Math.min(100, Math.max(0, percentage));
  const viewBox = 100;
  const radius = FOCUS_GAUGE_RADIUS;
  const circumference = focusGaugeCircumference(radius);
  const strokeDashoffset = focusGaugeStrokeDashoffset(pct, radius);

  return (
    <div
      className="focus-gauge"
      style={{ width: size, height: size }}
      role="img"
      aria-label={`${Math.round(pct)} percent ${label}`}
    >
      <svg viewBox={`0 0 ${viewBox} ${viewBox}`} className="focus-gauge-svg">
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
        <div className="focus-gauge-value">{pct.toFixed(0)}%</div>
        <div className="focus-gauge-label">{label}</div>
      </div>
    </div>
  );
}

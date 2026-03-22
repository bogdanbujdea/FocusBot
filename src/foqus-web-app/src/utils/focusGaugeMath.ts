export const FOCUS_GAUGE_RADIUS = 42;

export function focusGaugeCircumference(radius: number = FOCUS_GAUGE_RADIUS): number {
  return 2 * Math.PI * radius;
}

export function focusGaugeStrokeDashoffset(
  percentage: number,
  radius: number = FOCUS_GAUGE_RADIUS
): number {
  const c = focusGaugeCircumference(radius);
  const pct = Math.min(100, Math.max(0, percentage));
  return c - (pct / 100) * c;
}

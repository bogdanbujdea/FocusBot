import { render } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { FocusGauge } from "./FocusGauge";
import {
  FOCUS_GAUGE_RADIUS,
  focusGaugeCircumference,
  focusGaugeStrokeDashoffset,
} from "../utils/focusGaugeMath";

describe("focusGaugeStrokeDashoffset", () => {
  it("matches circumference math", () => {
    const c = focusGaugeCircumference(FOCUS_GAUGE_RADIUS);
    expect(focusGaugeStrokeDashoffset(0, FOCUS_GAUGE_RADIUS)).toBe(c);
    expect(focusGaugeStrokeDashoffset(100, FOCUS_GAUGE_RADIUS)).toBe(0);
    expect(focusGaugeStrokeDashoffset(70, FOCUS_GAUGE_RADIUS)).toBeCloseTo(
      c - 0.7 * c,
      5
    );
  });
});

describe("FocusGauge", () => {
  it("sets strokeDashoffset on the fill circle", () => {
    const { container } = render(<FocusGauge percentage={70} />);
    const fill = container.querySelector(".focus-gauge-fill");
    expect(fill).toBeTruthy();
    const c = focusGaugeCircumference(FOCUS_GAUGE_RADIUS);
    const expected = String(c - 0.7 * c);
    expect(fill?.getAttribute("stroke-dashoffset")).toBe(expected);
  });
});

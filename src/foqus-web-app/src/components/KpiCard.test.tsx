import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { KpiCard } from "./KpiCard";

describe("KpiCard", () => {
  it("renders label, value, and sublabel", () => {
    render(
      <KpiCard
        label="Test"
        value={42}
        sublabel="More"
      />
    );
    expect(screen.getByText("Test")).toBeInTheDocument();
    expect(screen.getByText("42")).toBeInTheDocument();
    expect(screen.getByText("More")).toBeInTheDocument();
  });

  it("applies aligned variant to card", () => {
    const { container } = render(
      <KpiCard label="X" value="v" variant="aligned" />
    );
    const card = container.querySelector(".kpi-card[data-variant='aligned']");
    expect(card).toBeTruthy();
  });

  it("renders FocusGauge for focus-score variant", () => {
    const { container } = render(
      <KpiCard
        variant="focus-score"
        label="Focus score"
        focusPercentage={55}
        focusDetails={<span>Details</span>}
      />
    );
    expect(screen.getByText("Details")).toBeInTheDocument();
    expect(container.querySelector(".focus-gauge-fill")).toBeTruthy();
  });
});

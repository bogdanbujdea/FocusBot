import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { SegmentedControl } from "./SegmentedControl";

const options = [
  { value: "a" as const, label: "Alpha" },
  { value: "b" as const, label: "Beta" },
];

describe("SegmentedControl", () => {
  it("highlights active option and calls onChange", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <SegmentedControl options={options} value="a" onChange={onChange} />
    );
    const beta = screen.getByRole("button", { name: "Beta" });
    expect(beta).toHaveAttribute("aria-pressed", "false");
    await user.click(beta);
    expect(onChange).toHaveBeenCalledWith("b");
  });
});

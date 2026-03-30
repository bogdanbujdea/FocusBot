import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { BYOKSetupModal } from "./BYOKSetupModal";

describe("BYOKSetupModal", () => {
  it("renders dialog when open", () => {
    render(<BYOKSetupModal open={true} onDismiss={vi.fn()} />);
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(
      screen.getByText(/Open the Foqus Windows app or browser extension/i)
    ).toBeInTheDocument();
    expect(screen.getByText(/encrypted locally on your device/i)).toBeInTheDocument();
  });

  it("renders nothing when closed", () => {
    const { container } = render(
      <BYOKSetupModal open={false} onDismiss={vi.fn()} />
    );
    expect(container.firstChild).toBeNull();
  });

  it("calls onDismiss when Got it is clicked", async () => {
    const onDismiss = vi.fn();
    const user = userEvent.setup();
    render(<BYOKSetupModal open={true} onDismiss={onDismiss} />);
    await user.click(screen.getByRole("button", { name: /^got it$/i }));
    expect(onDismiss).toHaveBeenCalledTimes(1);
  });
});

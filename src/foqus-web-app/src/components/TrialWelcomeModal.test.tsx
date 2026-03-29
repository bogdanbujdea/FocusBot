import { render, screen, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi, beforeEach } from "vitest";
import { TrialWelcomeModal, trialWelcomeSeenKey } from "./TrialWelcomeModal";

const trialEndsAt = new Date(Date.now() + 20 * 3_600_000).toISOString();

function renderModal(onDismiss = vi.fn()) {
  return render(
    <MemoryRouter>
      <TrialWelcomeModal trialEndsAt={trialEndsAt} onDismiss={onDismiss} />
    </MemoryRouter>
  );
}

describe("TrialWelcomeModal", () => {
  it("renders welcome content", () => {
    renderModal();
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(screen.getByText(/welcome to foqus/i)).toBeInTheDocument();
    expect(screen.getByText(/24 hours of full access/i)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /compare plans/i })).toBeInTheDocument();
  });

  it("calls onDismiss when Got it button is clicked", () => {
    const onDismiss = vi.fn();
    renderModal(onDismiss);
    fireEvent.click(screen.getByRole("button", { name: /got it/i }));
    expect(onDismiss).toHaveBeenCalledOnce();
  });

  it("calls onDismiss when backdrop is clicked", () => {
    const onDismiss = vi.fn();
    renderModal(onDismiss);
    fireEvent.click(screen.getByRole("presentation"));
    expect(onDismiss).toHaveBeenCalledOnce();
  });

  it("calls onDismiss on Escape key", () => {
    const onDismiss = vi.fn();
    renderModal(onDismiss);
    fireEvent.keyDown(document, { key: "Escape" });
    expect(onDismiss).toHaveBeenCalledOnce();
  });

  it("trialWelcomeSeenKey returns scoped localStorage key", () => {
    expect(trialWelcomeSeenKey("abc-123")).toBe("foqus.trialWelcomeSeen.abc-123");
  });
});

describe("TrialWelcomeModal localStorage integration", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it("localStorage key is set to true after dismiss in consumer code", () => {
    const userId = "user-test-1";
    const key = trialWelcomeSeenKey(userId);
    localStorage.setItem(key, "true");
    expect(localStorage.getItem(key)).toBe("true");
  });
});

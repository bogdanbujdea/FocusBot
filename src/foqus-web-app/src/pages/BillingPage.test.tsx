import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { BillingPage } from "./BillingPage";

const { mockApi, mockOpenCheckout } = vi.hoisted(() => ({
  mockApi: {
    getSubscriptionStatus: vi.fn(),
    createCustomerPortalSession: vi.fn(),
  },
  mockOpenCheckout: vi.fn(),
}));

vi.mock("../api/client", () => ({
  api: mockApi,
}));

vi.mock("../auth/useAuth", () => ({
  useAuth: () => ({
    user: { id: "user-uuid-1", email: "test@example.com" },
  }),
}));

vi.mock("../hooks/usePaddle", () => ({
  usePaddle: () => ({
    pricing: {
      plans: [
        {
          priceId: "pri_byok",
          name: "Cloud BYOK",
          description: "BYOK tier",
          unitAmountMinor: 199,
          currency: "USD",
          billingInterval: "month",
          planType: "cloud-byok",
        },
      ],
      clientToken: "ct",
      isSandbox: true,
    },
    loadError: null,
    ready: true,
    openCheckout: mockOpenCheckout,
  }),
}));

describe("BillingPage", () => {
  it("loads subscription and shows plans", async () => {
    mockApi.getSubscriptionStatus.mockResolvedValue({
      status: "none",
      planType: 0,
    });

    render(
      <MemoryRouter>
        <BillingPage />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText(/manage your subscription/i)).toBeInTheDocument();
    });

    expect(screen.getByRole("heading", { name: /plans/i })).toBeInTheDocument();
    expect(screen.getByText("Cloud BYOK")).toBeInTheDocument();
  });

  it("opens checkout when Subscribe is clicked", async () => {
    mockApi.getSubscriptionStatus.mockResolvedValue({
      status: "none",
      planType: 0,
    });
    const user = userEvent.setup();

    render(
      <MemoryRouter>
        <BillingPage />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /subscribe/i })).toBeInTheDocument();
    });

    await user.click(screen.getByRole("button", { name: /subscribe/i }));

    expect(mockOpenCheckout).toHaveBeenCalledWith(
      "pri_byok",
      "cloud-byok",
      "test@example.com",
      "user-uuid-1"
    );
  });

  it("opens portal when Manage subscription is clicked", async () => {
    mockApi.getSubscriptionStatus.mockResolvedValue({
      status: "active",
      planType: 1,
      currentPeriodEndsAt: "2030-01-01T00:00:00Z",
    });
    mockApi.createCustomerPortalSession.mockResolvedValue({
      ok: true,
      data: { url: "https://example.com/portal" },
    });
    const openSpy = vi.spyOn(window, "open").mockImplementation(() => null);
    const user = userEvent.setup();

    render(
      <MemoryRouter>
        <BillingPage />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: /manage subscription/i })
      ).toBeInTheDocument();
    });

    await user.click(screen.getByRole("button", { name: /manage subscription/i }));

    expect(mockApi.createCustomerPortalSession).toHaveBeenCalled();
    expect(openSpy).toHaveBeenCalledWith(
      "https://example.com/portal",
      "_blank",
      "noopener,noreferrer"
    );
    openSpy.mockRestore();
  });
});

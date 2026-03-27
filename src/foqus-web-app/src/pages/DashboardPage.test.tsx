import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import type { AnalyticsSummaryResponse } from "../api/types";
import { DashboardPage } from "./DashboardPage";

const emptySummary = (over: Partial<AnalyticsSummaryResponse> = {}): AnalyticsSummaryResponse => ({
  period: { from: "a", to: "b" },
  totalSessions: 0,
  totalFocusedSeconds: 0,
  totalDistractedSeconds: 0,
  averageFocusScorePercent: 0,
  totalDistractionCount: 0,
  totalContextSwitchCount: 0,
  averageSessionDurationSeconds: 0,
  longestSessionSeconds: 0,
  clientsActive: 0,
  ...over,
});

const { mockApi } = vi.hoisted(() => ({
  mockApi: {
    getAnalyticsSummary: vi.fn(),
    getSessions: vi.fn(),
    getActiveSession: vi.fn(),
    startSession: vi.fn(),
    pauseSession: vi.fn(),
    resumeSession: vi.fn(),
    endSession: vi.fn(),
  },
}));

vi.mock("../api/client", () => ({
  api: mockApi,
}));

vi.mock("../auth/useAuth", () => ({
  useAuth: () => ({
    session: { access_token: "test-token" },
  }),
}));

describe("DashboardPage", () => {
  it("shows start form when there is no active session", async () => {
    mockApi.getAnalyticsSummary.mockResolvedValue(emptySummary());
    mockApi.getSessions.mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 50,
    });
    mockApi.getActiveSession.mockResolvedValue(null);

    render(<DashboardPage />);

    await waitFor(() => {
      expect(
        screen.getByRole("heading", { name: /start a focus session/i })
      ).toBeInTheDocument();
    });
    expect(screen.queryByText(/active session/i)).not.toBeInTheDocument();
  });

  it("shows active panel when session is active", async () => {
    mockApi.getAnalyticsSummary.mockResolvedValue(emptySummary());
    mockApi.getSessions.mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 50,
    });
    mockApi.getActiveSession.mockResolvedValue({
      id: "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380a13",
      sessionTitle: "My task",
      startedAtUtc: new Date().toISOString(),
      totalPausedSeconds: 0,
      isPaused: false,
      source: "Web",
    });

    render(<DashboardPage />);

    await waitFor(() => {
      expect(screen.getByText("My task")).toBeInTheDocument();
    });
    expect(
      screen.getByRole("button", { name: /end session/i })
    ).toBeInTheDocument();
  });

  it("renders today KPIs from summary", async () => {
    mockApi.getAnalyticsSummary.mockResolvedValue(
      emptySummary({
        totalSessions: 2,
        totalFocusedSeconds: 3661,
        totalDistractedSeconds: 60,
        averageFocusScorePercent: 70,
        totalDistractionCount: 3,
      })
    );
    mockApi.getSessions.mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 50,
    });
    mockApi.getActiveSession.mockResolvedValue(null);

    render(<DashboardPage />);

    await waitFor(() => {
      expect(screen.getByText("1h 1m")).toBeInTheDocument();
    });
    expect(screen.getByText("70%")).toBeInTheDocument();
  });

  it("calls startSession when starting from the form", async () => {
    const user = userEvent.setup();
    mockApi.getAnalyticsSummary.mockResolvedValue(emptySummary());
    mockApi.getSessions.mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 50,
    });
    mockApi.getActiveSession.mockResolvedValue(null);
    mockApi.startSession.mockResolvedValue({
      ok: true,
      data: {
        id: "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380a14",
        sessionTitle: "New",
        startedAtUtc: new Date().toISOString(),
        totalPausedSeconds: 0,
        isPaused: false,
        source: "Web",
      },
    });

    render(<DashboardPage />);

    await waitFor(() => {
      expect(screen.getByLabelText(/task/i)).toBeInTheDocument();
    });
    await user.type(screen.getByLabelText("Task"), "Write docs");
    await user.click(
      screen.getByRole("button", { name: /start focus session/i })
    );

    await waitFor(() => {
      expect(mockApi.startSession).toHaveBeenCalledWith({
        sessionTitle: "Write docs",
        sessionContext: undefined,
      });
    });
  });

  it("shows session duration 30m for a simple completed session row", async () => {
    mockApi.getAnalyticsSummary.mockResolvedValue(emptySummary());
    mockApi.getSessions.mockResolvedValue({
      items: [
        {
          id: "e0eebc99-9c0b-4ef8-bb6d-6bb9bd380a15",
          sessionTitle: "Short",
          startedAtUtc: "2025-01-15T10:00:00.000Z",
          endedAtUtc: "2025-01-15T10:30:00.000Z",
          totalPausedSeconds: 0,
          isPaused: false,
          focusScorePercent: 80,
          source: "Web",
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 50,
    });
    mockApi.getActiveSession.mockResolvedValue(null);

    render(<DashboardPage />);

    await waitFor(() => {
      expect(screen.getByText("Short")).toBeInTheDocument();
    });
    expect(screen.getByText("30m")).toBeInTheDocument();
    expect(screen.getByText("80%")).toBeInTheDocument();
  });

  it("calls pauseSession when Pause is clicked", async () => {
    const user = userEvent.setup();
    mockApi.getAnalyticsSummary.mockResolvedValue(emptySummary());
    mockApi.getSessions.mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 50,
    });
    const sid = "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380a16";
    mockApi.getActiveSession.mockResolvedValue({
      id: sid,
      sessionTitle: "Active",
      startedAtUtc: new Date().toISOString(),
      totalPausedSeconds: 0,
      isPaused: false,
      source: "Web",
    });
    mockApi.pauseSession.mockResolvedValue({
      ok: true,
      data: {
        id: sid,
        sessionTitle: "Active",
        startedAtUtc: new Date().toISOString(),
        pausedAtUtc: new Date().toISOString(),
        totalPausedSeconds: 0,
        isPaused: true,
        source: "Web",
      },
    });

    render(<DashboardPage />);

    await waitFor(() => {
      expect(screen.getByText("Active")).toBeInTheDocument();
    });
    await user.click(screen.getByRole("button", { name: /^pause$/i }));

    await waitFor(() => {
      expect(mockApi.pauseSession).toHaveBeenCalledWith(sid);
    });
  });

  it("calls endSession with focused wall time when ending", async () => {
    const user = userEvent.setup();
    const endMs = new Date("2025-01-20T12:10:00.000Z").getTime();
    const nowSpy = vi.spyOn(Date, "now").mockReturnValue(endMs);
    mockApi.getAnalyticsSummary.mockResolvedValue(emptySummary());
    mockApi.getSessions.mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 50,
    });
    const started = new Date("2025-01-20T12:00:00.000Z").toISOString();
    const sid = "a1eebc99-9c0b-4ef8-bb6d-6bb9bd380a17";
    mockApi.getActiveSession.mockResolvedValue({
      id: sid,
      sessionTitle: "End me",
      startedAtUtc: started,
      totalPausedSeconds: 0,
      isPaused: false,
      source: "Web",
    });
    mockApi.endSession.mockResolvedValue({
      ok: true,
      data: {
        id: sid,
        sessionTitle: "End me",
        startedAtUtc: started,
        endedAtUtc: new Date().toISOString(),
        totalPausedSeconds: 0,
        isPaused: false,
        focusScorePercent: 100,
        source: "Web",
      },
    });

    vi.setSystemTime(new Date("2025-01-20T12:10:00.000Z"));

    render(<DashboardPage />);

    await waitFor(() => {
      expect(screen.getByText("End me")).toBeInTheDocument();
    });
    await user.click(screen.getByRole("button", { name: /end session/i }));

    await waitFor(() => {
      expect(mockApi.endSession).toHaveBeenCalled();
    });
    const call = mockApi.endSession.mock.calls[0];
    expect(call[0]).toBe(sid);
    expect(call[1]).toMatchObject({
      focusScorePercent: 100,
      distractedSeconds: 0,
      distractionCount: 0,
      contextSwitchCount: 0,
    });
    expect(call[1].focusedSeconds).toBe(600);

    nowSpy.mockRestore();
  });
});

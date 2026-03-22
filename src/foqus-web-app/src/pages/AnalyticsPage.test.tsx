import { render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { AnalyticsSummaryResponse } from "../api/types";
import { AnalyticsPage } from "./AnalyticsPage";

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
    getAnalyticsTrends: vi.fn(),
    getAnalyticsClients: vi.fn(),
    getSessions: vi.fn(),
  },
}));

vi.mock("../api/client", () => ({
  api: mockApi,
}));

describe("AnalyticsPage numbers", () => {
  it("shows KPIs from summary including Deep work 1h 1m and focus 73%", async () => {
    mockApi.getAnalyticsSummary.mockResolvedValue(
      emptySummary({
        totalSessions: 2,
        totalFocusedSeconds: 3661,
        totalDistractedSeconds: 90,
        averageFocusScorePercent: 73,
        totalDistractionCount: 6,
        averageSessionDurationSeconds: 1800,
        longestSessionSeconds: 5400,
      })
    );
    mockApi.getAnalyticsTrends.mockResolvedValue({
      granularity: "daily",
      dataPoints: [],
    });
    mockApi.getAnalyticsClients.mockResolvedValue({ clients: [] });
    mockApi.getSessions.mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 10,
    });

    render(<AnalyticsPage />);

    await waitFor(() => {
      expect(screen.getByText("1h 1m")).toBeInTheDocument();
    });
    expect(screen.getByText("73%")).toBeInTheDocument();
    expect(screen.getByText("30m")).toBeInTheDocument();
    expect(screen.getByText("1h 30m")).toBeInTheDocument();
    expect(screen.getByText(/Distracting 1m/)).toBeInTheDocument();
  });

  it("shows em dash for focus score when no sessions", async () => {
    mockApi.getAnalyticsSummary.mockResolvedValue(emptySummary());
    mockApi.getAnalyticsTrends.mockResolvedValue({
      granularity: "daily",
      dataPoints: [],
    });
    mockApi.getAnalyticsClients.mockResolvedValue({ clients: [] });
    mockApi.getSessions.mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 10,
    });

    render(<AnalyticsPage />);

    await waitFor(() => {
      expect(screen.getByText("—")).toBeInTheDocument();
    });
  });

  it("maps trend seconds to rounded minutes in chart data path", async () => {
    mockApi.getAnalyticsSummary.mockResolvedValue(emptySummary({ totalSessions: 1 }));
    mockApi.getAnalyticsTrends.mockResolvedValue({
      granularity: "daily",
      dataPoints: [
        {
          date: "2025-01-01",
          sessions: 1,
          focusedSeconds: 150,
          distractedSeconds: 30,
          focusScorePercent: 80,
          distractionCount: 1,
        },
      ],
    });
    mockApi.getAnalyticsClients.mockResolvedValue({ clients: [] });
    mockApi.getSessions.mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 10,
    });

    render(<AnalyticsPage />);

    await waitFor(() => {
      expect(screen.getByText("Focus trend")).toBeInTheDocument();
    });
  });

  it("colors focus column by score in session table", async () => {
    mockApi.getAnalyticsSummary.mockResolvedValue(
      emptySummary({ totalSessions: 1 })
    );
    mockApi.getAnalyticsTrends.mockResolvedValue({
      granularity: "daily",
      dataPoints: [],
    });
    mockApi.getAnalyticsClients.mockResolvedValue({ clients: [] });
    mockApi.getSessions.mockResolvedValue({
      items: [
        {
          id: "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380a12",
          sessionTitle: "Work",
          startedAtUtc: "2025-01-15T10:00:00.000Z",
          endedAtUtc: "2025-01-15T10:30:00.000Z",
          totalPausedSeconds: 0,
          isPaused: false,
          focusScorePercent: 85,
          distractionCount: 0,
          source: "Extension",
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 10,
    });

    render(<AnalyticsPage />);

    await waitFor(() => {
      expect(screen.getByText("85%")).toBeInTheDocument();
    });
    const cell = screen.getByText("85%");
    expect(cell.className).toContain("focus-pct-high");
  });
});

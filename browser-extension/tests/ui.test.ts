import { describe, expect, it } from "vitest";
import {
  getPlanLabelFromServerPlanType,
  isByokKeyMissing,
  isExpiredTrial,
  isTrialBannerVisible,
  mapBackendPlanType,
  shouldShowByokPrompt
} from "../src/shared/subscription";
import type { Settings } from "../src/shared/types";

const baseSettings: Settings = {
  plan: "trial",
  openAiApiKey: "",
  classifierModel: "gpt-4o-mini",
  onboardingCompleted: false,
  subscriptionStatus: "trial",
  serverPlanType: 0
};

describe("phase 3 subscription ui logic", () => {
  it("maps backend planType 0 to trial", () => {
    expect(mapBackendPlanType(0)).toBe("trial");
  });

  it("trial banner renders only when trial + planType 0 + future trial end + popup", () => {
    const now = Date.UTC(2026, 2, 30, 10, 0, 0);
    const settings: Settings = {
      ...baseSettings,
      trialEndsAt: new Date(now + 60_000).toISOString()
    };

    expect(isTrialBannerVisible(settings, true, now)).toBe(true);
    expect(isTrialBannerVisible(settings, false, now)).toBe(false);
  });

  it("trial banner hidden for paid plans", () => {
    const now = Date.UTC(2026, 2, 30, 10, 0, 0);
    const settings: Settings = {
      ...baseSettings,
      plan: "cloud-byok",
      serverPlanType: 1,
      subscriptionStatus: "active",
      trialEndsAt: new Date(now + 60_000).toISOString()
    };

    expect(isTrialBannerVisible(settings, true, now)).toBe(false);
  });

  it("options summary exposes trial label", () => {
    expect(getPlanLabelFromServerPlanType(0)).toBe("Trial (24h)");
  });

  it("shows BYOK prompt only for cloud-byok without key", () => {
    expect(shouldShowByokPrompt("cloud-byok", "")).toBe(true);
    expect(shouldShowByokPrompt("cloud-byok", "sk-test")).toBe(false);
    expect(shouldShowByokPrompt("trial", "")).toBe(false);
  });

  it("flags missing key only for signed-in BYOK users", () => {
    const byokSettings: Settings = {
      ...baseSettings,
      plan: "cloud-byok",
      serverPlanType: 1,
      subscriptionStatus: "active",
      openAiApiKey: ""
    };
    expect(isByokKeyMissing(byokSettings, true)).toBe(true);
    expect(isByokKeyMissing(byokSettings, false)).toBe(false);
    expect(isByokKeyMissing({ ...byokSettings, openAiApiKey: "sk-test" }, true)).toBe(false);
  });

  it("expired trial reports no active plan condition", () => {
    const now = Date.UTC(2026, 2, 30, 10, 0, 0);
    const past = new Date(now - 60_000).toISOString();

    expect(isExpiredTrial("trial", 0, past, now)).toBe(true);
    expect(isExpiredTrial("expired", 0, undefined, now)).toBe(true);
    expect(isExpiredTrial("active", 1, past, now)).toBe(false);
  });
});

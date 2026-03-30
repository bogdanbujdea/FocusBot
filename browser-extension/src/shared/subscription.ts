import type { PlanType, Settings } from "./types";

export type BackendPlanType = 0 | 1 | 2;

export const mapBackendPlanType = (planType: number): PlanType | null => {
  if (planType === 0) return "trial";
  if (planType === 1) return "cloud-byok";
  if (planType === 2) return "cloud-managed";
  return null;
};

export const isFutureIsoDate = (isoDate: string | undefined, nowMs: number = Date.now()): boolean => {
  if (!isoDate) return false;
  const parsed = Date.parse(isoDate);
  if (Number.isNaN(parsed)) return false;
  return parsed > nowMs;
};

export const isTrialBannerVisible = (
  settings: Settings,
  compact: boolean,
  nowMs: number = Date.now()
): boolean =>
  compact &&
  settings.subscriptionStatus === "trial" &&
  settings.serverPlanType === 0 &&
  isFutureIsoDate(settings.trialEndsAt, nowMs);

export const getPlanLabelFromServerPlanType = (serverPlanType: number | undefined): string => {
  if (serverPlanType === 0) return "Trial (24h)";
  if (serverPlanType === 1) return "Cloud BYOK";
  if (serverPlanType === 2) return "Cloud Managed";
  return "No active plan";
};

export const isExpiredTrial = (
  status: string | undefined,
  serverPlanType: number | undefined,
  trialEndsAt: string | undefined,
  nowMs: number = Date.now()
): boolean => {
  if (serverPlanType !== 0) return false;
  if (status === "expired") return true;
  if (!trialEndsAt) return false;
  const parsed = Date.parse(trialEndsAt);
  if (Number.isNaN(parsed)) return false;
  return parsed <= nowMs;
};

export const shouldShowByokPrompt = (plan: PlanType, openAiApiKey: string): boolean =>
  plan === "cloud-byok" && openAiApiKey.trim().length === 0;

export const isByokKeyMissing = (settings: Settings, isAuthenticated: boolean): boolean => {
  if (!isAuthenticated) return false;
  const isByokPlan = settings.serverPlanType === 1 || settings.plan === "cloud-byok";
  return isByokPlan && settings.openAiApiKey.trim().length === 0;
};

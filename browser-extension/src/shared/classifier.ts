import type { ClassificationResult, Settings } from "./types";
import { planRequiresApiKey } from "./types";
import { classifyViaWebApi } from "./apiClient";

/** Maps Web API /classify score (1–10) to UI classification. */
export const mapApiScoreToClassification = (score: number): "aligned" | "neutral" | "distracting" => {
  if (score > 5) return "aligned";
  if (score < 5) return "distracting";
  return "neutral";
};

export const classifyDesktopApp = async (
  settings: Settings,
  taskText: string,
  processName: string,
  windowTitle: string,
  taskHints?: string,
  _timeoutMs = 8000
): Promise<ClassificationResult> => {
  void _timeoutMs;
  if (planRequiresApiKey(settings.plan) && !settings.openAiApiKey.trim()) {
    throw new Error("OpenAI API key is required.");
  }

  const web = await classifyViaWebApi(
    {
      taskText,
      taskHints,
      processName,
      windowTitle,
      modelId: settings.classifierModel?.trim() || undefined
    },
    {
      byokApiKey: planRequiresApiKey(settings.plan) ? settings.openAiApiKey.trim() : undefined
    }
  );

  const result: ClassificationResult = {
    classification: mapApiScoreToClassification(web.score),
    confidence: Math.max(0, Math.min(1, web.score / 10)),
    reason: web.reason,
    score: web.score
  };

  return result;
};

export const classifyPage = async (
  settings: Settings,
  taskText: string,
  url: string,
  title: string,
  _timeoutMs = 8000,
  taskHints?: string
): Promise<ClassificationResult> => {
  void _timeoutMs;
  if (planRequiresApiKey(settings.plan) && !settings.openAiApiKey.trim()) {
    throw new Error("OpenAI API key is required.");
  }

  const web = await classifyViaWebApi(
    {
      taskText,
      taskHints,
      url,
      pageTitle: title,
      modelId: settings.classifierModel?.trim() || undefined
    },
    {
      byokApiKey: planRequiresApiKey(settings.plan) ? settings.openAiApiKey.trim() : undefined
    }
  );

  const result: ClassificationResult = {
    classification: mapApiScoreToClassification(web.score),
    confidence: Math.max(0, Math.min(1, web.score / 10)),
    reason: web.reason,
    score: web.score
  };

  return result;
};

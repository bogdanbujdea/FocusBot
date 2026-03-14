import type { ClassificationResult, Settings } from "./types";
import { loadClassificationCache, saveClassificationCache } from "./storage";

const cacheKey = (taskText: string, url: string): string =>
  `${taskText.trim().toLowerCase()}::${url.trim().toLowerCase()}`;

const classifierPrompt = (taskText: string, url: string, title: string): string =>
  [
    "You classify whether a browser page is aligned with a deep-work task.",
    "Return strict JSON only with keys: classification, confidence, reason.",
    'classification must be either "aligned" or "distracting".',
    "confidence must be a number from 0 to 1.",
    "",
    `Task: ${taskText}`,
    `URL: ${url}`,
    `Title: ${title || "N/A"}`
  ].join("\n");

const parseClassification = (raw: string): ClassificationResult => {
  try {
    const parsed = JSON.parse(raw) as Partial<ClassificationResult>;
    if (parsed.classification === "aligned" || parsed.classification === "distracting") {
      return {
        classification: parsed.classification,
        confidence: typeof parsed.confidence === "number" ? Math.max(0, Math.min(1, parsed.confidence)) : 0.5,
        reason: parsed.reason
      };
    }
  } catch {
    // Ignore parse failure and fallback.
  }

  const lowered = raw.toLowerCase();
  const classification = lowered.includes("distracting") ? "distracting" : "aligned";
  return {
    classification,
    confidence: 0.5,
    reason: "Fallback parser used due to non-JSON response."
  };
};

export const classifyPage = async (
  settings: Settings,
  taskText: string,
  url: string,
  title: string,
  timeoutMs = 8000
): Promise<ClassificationResult> => {
  if (!settings.openAiApiKey.trim()) {
    throw new Error("OpenAI API key is required.");
  }

  const key = cacheKey(taskText, url);
  const cache = await loadClassificationCache();
  const cached = cache[key];
  if (cached) {
    return {
      classification: cached.classification,
      confidence: cached.confidence,
      reason: cached.reason
    };
  }

  const timeoutController = new AbortController();
  const timeoutHandle = setTimeout(() => timeoutController.abort(), timeoutMs);

  let response: Response;
  try {
    response = await fetch("https://api.openai.com/v1/chat/completions", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${settings.openAiApiKey}`
      },
      body: JSON.stringify({
        model: settings.classifierModel,
        temperature: 0.1,
        response_format: { type: "json_object" },
        messages: [
          {
            role: "system",
            content:
              "You are an alignment classifier for browser deep work. Keep output compact and strict JSON only."
          },
          {
            role: "user",
            content: classifierPrompt(taskText, url, title)
          }
        ]
      }),
      signal: timeoutController.signal
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError") {
      throw new Error("Classification timed out. Please verify network connectivity and model access.");
    }
    throw error;
  } finally {
    clearTimeout(timeoutHandle);
  }

  if (!response.ok) {
    throw new Error(`OpenAI classification failed (${response.status}).`);
  }

  const payload = (await response.json()) as {
    choices?: Array<{ message?: { content?: string } }>;
  };
  const content = payload.choices?.[0]?.message?.content ?? "";
  const parsed = parseClassification(content);

  cache[key] = {
    classification: parsed.classification,
    confidence: parsed.confidence,
    reason: parsed.reason,
    createdAt: new Date().toISOString()
  };
  await saveClassificationCache(cache);

  return parsed;
};

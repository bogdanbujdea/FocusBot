import type { ClassificationResult, Settings } from "./types";
import { planUsesDirectClassification } from "./types";
import { loadClassificationCache, saveClassificationCache } from "./storage";
import { classifyViaWebApi } from "./apiClient";

const cacheKey = (taskText: string, url: string): string =>
  `${taskText.trim().toLowerCase()}::${url.trim().toLowerCase()}`;

const classifierPrompt = (taskText: string, url: string, title: string, taskHints?: string): string => {
  const lines = [
    "You classify whether a browser page is aligned with the user's stated task.",
    "The task can be any kind of work (e.g. marketing, research, creative), a break, or entertainment. If the user's task is to use this site or this type of content (including social, streaming, entertainment), classify as aligned. Do not treat sites as distracting just because they are social or entertainment—only when they do not match the task.",
    "Return strict JSON only with keys: classification, confidence, reason.",
    'classification must be either "aligned" or "distracting".',
    "confidence must be a number from 0 to 1.",
    'reason: when distracting, only the reason clause that follows "because" (e.g. "it is a social feed unrelated to your task", not "this page is not aligned because..."); when aligned, a brief explanation.',
    "",
    `Task: ${taskText}`
  ];
  
  if (taskHints) {
    lines.push(`Context: ${taskHints}`);
  }
  
  lines.push(`URL: ${url}`);
  lines.push(`Title: ${title || "N/A"}`);
  
  return lines.join("\n");
};

const parseClassification = (raw: string): ClassificationResult => {
  try {
    const parsed = JSON.parse(raw) as Partial<ClassificationResult>;
    if (
      parsed.classification === "aligned" ||
      parsed.classification === "neutral" ||
      parsed.classification === "distracting"
    ) {
      return {
        classification: parsed.classification,
        confidence: typeof parsed.confidence === "number" ? Math.max(0, Math.min(1, parsed.confidence)) : 0.5,
        reason: parsed.reason,
        score: typeof parsed.score === "number" ? Math.max(1, Math.min(10, parsed.score)) : undefined
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

/** Maps Web API /classify score (1–10) to UI classification. */
export const mapApiScoreToClassification = (score: number): "aligned" | "neutral" | "distracting" => {
  if (score > 5) return "aligned";
  if (score < 5) return "distracting";
  return "neutral";
};

const desktopClassifierPrompt = (taskText: string, processName: string, windowTitle: string, taskHints?: string): string => {
  const lines = [
    "You classify whether a desktop application is aligned with the user's stated task.",
    "The task can be any kind of work (e.g. coding, marketing, research, creative), a break, or entertainment.",
    "Return strict JSON only with keys: classification, confidence, reason.",
    'classification must be either "aligned" or "distracting".',
    "confidence must be a number from 0 to 1.",
    'reason: when distracting, only the reason clause that follows "because"; when aligned, a brief explanation.',
    "",
    `Task: ${taskText}`
  ];
  
  if (taskHints) {
    lines.push(`Context: ${taskHints}`);
  }
  
  lines.push(`Application: ${processName}`);
  lines.push(`Window Title: ${windowTitle || "N/A"}`);
  
  return lines.join("\n");
};

const desktopCacheKey = (taskText: string, processName: string, windowTitle: string): string =>
  `${taskText.trim().toLowerCase()}::desktop::${processName.trim().toLowerCase()}::${windowTitle.trim().toLowerCase()}`;

export const classifyDesktopApp = async (
  settings: Settings,
  taskText: string,
  processName: string,
  windowTitle: string,
  taskHints?: string,
  timeoutMs = 8000
): Promise<ClassificationResult> => {
  if (!planUsesDirectClassification(settings.plan)) {
    const web = await classifyViaWebApi({
      taskText,
      taskHints,
      processName,
      windowTitle
    });
    return {
      classification: mapApiScoreToClassification(web.score),
      confidence: Math.max(0, Math.min(1, web.score / 10)),
      reason: web.reason,
      score: web.score
    };
  }

  if (!settings.openAiApiKey.trim()) {
    throw new Error("OpenAI API key is required.");
  }

  const key = desktopCacheKey(taskText, processName, windowTitle);
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
              "You decide whether a desktop application matches the user's stated task. The task can be any kind of work or a break. Do not judge the task—only whether the application serves that task. Keep output compact and strict JSON only."
          },
          {
            role: "user",
            content: desktopClassifierPrompt(taskText, processName, windowTitle, taskHints)
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

export const classifyPage = async (
  settings: Settings,
  taskText: string,
  url: string,
  title: string,
  timeoutMs = 8000,
  taskHints?: string
): Promise<ClassificationResult> => {
  if (!planUsesDirectClassification(settings.plan)) {
    const web = await classifyViaWebApi({
      taskText,
      taskHints,
      url,
      pageTitle: title
    });
    return {
      classification: mapApiScoreToClassification(web.score),
      confidence: Math.max(0, Math.min(1, web.score / 10)),
      reason: web.reason,
      score: web.score
    };
  }

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
              "You decide whether the current page matches the user's stated task. The task can be any kind of work (including marketing, social, creative) or a break (e.g. watch movies, browse social). Do not judge the task as productive or not—only whether the page serves that task. Keep output compact and strict JSON only."
          },
          {
            role: "user",
            content: classifierPrompt(taskText, url, title, taskHints)
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

const OPENAI_CHAT_COMPLETIONS_URL = "https://api.openai.com/v1/chat/completions";

const toUserFriendlyErrorMessage = (rawMessage: string): string => {
  if (!rawMessage.trim()) return rawMessage;
  if (rawMessage.includes("429")) {
    return "You've exceeded your API quota. Check your plan and billing in your provider's dashboard.";
  }

  const lowered = rawMessage.toLowerCase();
  if (
    lowered.includes("invalid_api_key") ||
    lowered.includes("incorrect api key") ||
    lowered.includes("invalid_request_error") ||
    (rawMessage.includes("{") && rawMessage.includes('"error"'))
  ) {
    return "The API key may be incorrect or you may not have sufficient funds. Check your key and billing in your provider's dashboard.";
  }

  return rawMessage;
};

const readOpenAiErrorMessage = async (response: Response): Promise<string> => {
  try {
    const payload = (await response.json()) as { error?: { message?: string } };
    const message = payload.error?.message;
    if (message) return message;
  } catch {
    // ignore
  }

  try {
    const text = await response.text();
    if (text) return text;
  } catch {
    // ignore
  }

  return `OpenAI request failed (${response.status}).`;
};

export type OpenAiValidationResult = { ok: true } | { ok: false; error: string };

export const validateOpenAiKey = async (args: {
  apiKey: string;
  model: string;
  timeoutMs?: number;
}): Promise<OpenAiValidationResult> => {
  const apiKey = args.apiKey.trim();
  if (!apiKey) return { ok: false, error: "Please enter an API key." };

  const timeoutController = new AbortController();
  const timeoutHandle = setTimeout(() => timeoutController.abort(), args.timeoutMs ?? 8000);

  try {
    const response = await fetch(OPENAI_CHAT_COMPLETIONS_URL, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${apiKey}`
      },
      body: JSON.stringify({
        model: args.model,
        temperature: 0.1,
        max_completion_tokens: 4,
        messages: [{ role: "user", content: "Ping" }]
      }),
      signal: timeoutController.signal
    });

    if (!response.ok) {
      const errorMessage = await readOpenAiErrorMessage(response);
      return { ok: false, error: toUserFriendlyErrorMessage(errorMessage) };
    }

    const payload = (await response.json()) as {
      choices?: Array<{ message?: { content?: string } }>;
    };
    const content = payload.choices?.[0]?.message?.content ?? "";
    if (!content.trim()) {
      return { ok: false, error: "No response from API." };
    }

    return { ok: true };
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError") {
      return { ok: false, error: "Validation timed out. Please verify network connectivity and model access." };
    }

    return {
      ok: false,
      error: toUserFriendlyErrorMessage(error instanceof Error ? error.message : "Unable to validate API key.")
    };
  } finally {
    clearTimeout(timeoutHandle);
  }
};


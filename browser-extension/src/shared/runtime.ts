import type { RuntimeRequest, RuntimeResponse } from "./types";

export const sendRuntimeRequest = async <T>(request: RuntimeRequest): Promise<RuntimeResponse<T>> =>
  chrome.runtime.sendMessage(request) as Promise<RuntimeResponse<T>>;

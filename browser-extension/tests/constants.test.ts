import { describe, expect, it } from "vitest";
import { WINDOWS_STORE_APP_URL, INSTALL_APP_MESSAGE } from "../src/shared/constants";

describe("constants", () => {
  it("WINDOWS_STORE_APP_URL is defined and is a valid HTTPS URL", () => {
    expect(WINDOWS_STORE_APP_URL).toBeDefined();
    expect(typeof WINDOWS_STORE_APP_URL).toBe("string");
    expect(WINDOWS_STORE_APP_URL).toMatch(/^https:\/\//);
    expect(() => new URL(WINDOWS_STORE_APP_URL)).not.toThrow();
  });

  it("INSTALL_APP_MESSAGE contains the expected copy about desktop app and Microsoft Store", () => {
    expect(INSTALL_APP_MESSAGE).toContain("Track the Windows apps you use");
    expect(INSTALL_APP_MESSAGE).toContain("focus alignment");
    expect(INSTALL_APP_MESSAGE).toContain("Microsoft Store");
  });
});

// Edited on Sep 6, 2026 @ 12:50:30 -> Extend deepLink to use providerRegistry for search URLs and provider actions
import { providerRegistry } from "./providerRegistry";
import { ProviderSource } from "./types";

/**
 * Builds the deep search URL for Karaoke Version using providerRegistry.
 */
export function getKaraokeVersionSearchUrl(query: string): string {
  return providerRegistry.getProviderBySource("Karaoke Version")?.buildSearchUrl(query) ??
    "https://www.karaoke-version.com/";
}

/**
 * Builds the deep search URL for Party Tyme Karaoke using providerRegistry.
 */
export function getPartyTymeSearchUrl(query: string): string {
  return providerRegistry.getProviderBySource("Party Tyme")?.buildSearchUrl(query) ??
    "https://www.partytyme.net/";
}

/**
 * Builds the deep search URL for Karaoke.com using providerRegistry.
 */
export function getKaraokeDotComSearchUrl(query: string): string {
  return providerRegistry.getProviderBySource("Karaoke.com")?.buildSearchUrl(query) ??
    "https://karaoke.com/";
}

/**
 * Builds the deep search URL for Sunfly Karaoke using providerRegistry.
 */
export function getSunflySearchUrl(query: string): string {
  return providerRegistry.getProviderBySource("Sunfly")?.buildSearchUrl(query) ??
    "https://www.sunflykaraoke.com/";
}

/**
 * Builds search URL for any registered provider source.
 */
export function getProviderSearchUrl(source: ProviderSource, query: string): string {
  const provider = providerRegistry.getProviderBySource(source);
  return provider ? provider.buildSearchUrl(query) : "";
}

/**
 * Opens an external URL in the user's default browser.
 * Supports web browser environment (window.open) and Node.js desktop environment (child_process).
 */
export function openExternalUrl(url: string): void {
  if (!url) return;

  if (typeof window !== "undefined" && window.open) {
    window.open(url, "_blank", "noopener,noreferrer");
    return;
  }

  // Node.js fallback
  try {
    // eslint-disable-next-line @typescript-eslint/no-var-requires
    const { exec } = require("child_process");
    const cmd = process.platform === "win32"
      ? `start "" "${url}"`
      : process.platform === "darwin"
      ? `open "${url}"`
      : `xdg-open "${url}"`;
    exec(cmd);
  } catch (err) {
    console.error("Failed to open external URL:", url, err);
  }
}

/**
 * Directly executes a search for a given provider source and query string.
 */
export function searchProvider(source: ProviderSource, query: string): void {
  const url = getProviderSearchUrl(source, query);
  if (url) {
    openExternalUrl(url);
  }
}

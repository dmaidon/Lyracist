// Edited on Sep 6, 2026 @ 10:46:00 -> Add Karaoke.com and Sunfly deep link search providers

/**
 * Builds the deep search URL for Karaoke Version.
 * @param query Song title, artist, or keywords.
 */
export function getKaraokeVersionSearchUrl(query: string): string {
  const trimmed = query.trim();
  if (!trimmed) {
    return "https://www.karaoke-version.com/";
  }
  return `https://www.karaoke-version.com/search.html?q=${encodeURIComponent(trimmed)}`;
}

/**
 * Builds the deep search URL for Party Tyme Karaoke.
 * @param query Song title, artist, or keywords.
 */
export function getPartyTymeSearchUrl(query: string): string {
  const trimmed = query.trim();
  if (!trimmed) {
    return "https://www.partytyme.net/";
  }
  return `https://www.partytyme.net/search?q=${encodeURIComponent(trimmed)}`;
}

/**
 * Builds the deep search URL for Karaoke.com.
 * @param query Song title, artist, or keywords.
 */
export function getKaraokeDotComSearchUrl(query: string): string {
  const trimmed = query.trim();
  if (!trimmed) {
    return "https://karaoke.com/";
  }
  return `https://karaoke.com/search?type=product&q=${encodeURIComponent(trimmed)}`;
}

/**
 * Builds the deep search URL for Sunfly Karaoke.
 * @param query Song title, artist, or keywords.
 */
export function getSunflySearchUrl(query: string): string {
  const trimmed = query.trim();
  if (!trimmed) {
    return "https://www.sunflykaraoke.com/";
  }
  return `https://www.sunflykaraoke.com/catalogsearch/result/?q=${encodeURIComponent(trimmed)}`;
}

/**
 * Opens an external URL in the user's default browser.
 * Supports web browser environment (window.open) and Node.js desktop environment (child_process).
 */
export function openExternalUrl(url: string): void {
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

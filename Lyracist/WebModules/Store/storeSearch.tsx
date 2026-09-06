// Edited on Sep 6, 2026 @ 11:10:00 -> Set in-memory referrer hint on provider search button click
import React, { useState } from "react";
import {
  getKaraokeVersionSearchUrl,
  getPartyTymeSearchUrl,
  getKaraokeDotComSearchUrl,
  getSunflySearchUrl,
  openExternalUrl,
} from "./deepLink";
import { setReferrerHint } from "./importMetadata";
import { TrackSource } from "./types";

interface StoreSearchProps {
  onSearchExecuted?: (provider: string, query: string) => void;
}

export const StoreSearch: React.FC<StoreSearchProps> = ({ onSearchExecuted }) => {
  const [query, setQuery] = useState("");
  const [showAdditional, setShowAdditional] = useState(false);

  const handleSearch = (provider: string, getUrl: (q: string) => string) => {
    setReferrerHint(provider as TrackSource);
    const url = getUrl(query);
    openExternalUrl(url);
    if (onSearchExecuted) onSearchExecuted(provider, query);
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Enter") {
      handleSearch("Karaoke Version", getKaraokeVersionSearchUrl);
    }
  };

  return (
    <div style={styles.card}>
      <h3 style={styles.heading}>Search Licensed Karaoke Stores</h3>
      <p style={styles.subtext}>
        Search for licensed backing tracks across authorized stores. Clicking will open the official store in your web browser.
      </p>

      {/* Main Search Row */}
      <div style={styles.searchRow}>
        <input
          type="text"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="Enter song title, artist, or keywords (e.g. 'Bohemian Rhapsody')..."
          style={styles.input}
        />

        <button
          onClick={() => handleSearch("Karaoke Version", getKaraokeVersionSearchUrl)}
          style={{ ...styles.button, backgroundColor: "#e65100" }}
          title="Search on Karaoke-Version.com"
        >
          🔍 Search Karaoke Version
        </button>

        <button
          onClick={() => handleSearch("Party Tyme", getPartyTymeSearchUrl)}
          style={{ ...styles.button, backgroundColor: "#00838f" }}
          title="Search on PartyTyme.net"
        >
          🎵 Search Party Tyme
        </button>

        <button
          onClick={() => setShowAdditional(!showAdditional)}
          style={styles.toggleButton}
          title="Toggle Additional Store Providers"
        >
          {showAdditional ? "▲ Less Providers" : "▼ More Providers"}
        </button>
      </div>

      <div style={styles.tipRow}>
        <span style={styles.hint}>Tip: Press Enter in the search box to search Karaoke Version instantly.</span>
      </div>

      {/* Additional Providers Section */}
      {showAdditional && (
        <div style={styles.additionalSection}>
          <div style={styles.additionalHeader}>
            <span style={styles.additionalTitle}>Additional Licensed Providers:</span>
          </div>
          <div style={styles.additionalRow}>
            <button
              onClick={() => handleSearch("Karaoke.com", getKaraokeDotComSearchUrl)}
              style={{ ...styles.button, backgroundColor: "#4a148c" }}
              title="Search on Karaoke.com"
            >
              🎤 Search Karaoke.com
            </button>

            <button
              onClick={() => handleSearch("Sunfly", getSunflySearchUrl)}
              style={{ ...styles.button, backgroundColor: "#1565c0" }}
              title="Search on SunflyKaraoke.com"
            >
              ☀️ Search Sunfly Karaoke
            </button>
          </div>
        </div>
      )}
    </div>
  );
};

const styles: Record<string, React.CSSProperties> = {
  card: {
    backgroundColor: "rgba(255, 255, 255, 0.05)",
    border: "1px solid rgba(255, 255, 255, 0.12)",
    borderRadius: "10px",
    padding: "20px",
    marginBottom: "18px",
  },
  heading: {
    margin: "0 0 6px 0",
    fontSize: "1.15rem",
    fontWeight: 600,
    color: "#ffffff",
  },
  subtext: {
    margin: "0 0 14px 0",
    fontSize: "0.85rem",
    color: "#a0aec0",
    lineHeight: 1.4,
  },
  searchRow: {
    display: "flex",
    gap: "10px",
    alignItems: "center",
    marginBottom: "8px",
    flexWrap: "wrap",
  },
  input: {
    flex: 1,
    minWidth: "220px",
    padding: "10px 14px",
    borderRadius: "6px",
    border: "1px solid rgba(255, 255, 255, 0.2)",
    backgroundColor: "rgba(0, 0, 0, 0.35)",
    color: "#ffffff",
    fontSize: "0.95rem",
    outline: "none",
  },
  button: {
    padding: "10px 18px",
    borderRadius: "6px",
    border: "none",
    color: "#ffffff",
    fontWeight: 600,
    fontSize: "0.9rem",
    cursor: "pointer",
    whiteSpace: "nowrap",
    transition: "opacity 0.2s ease",
  },
  toggleButton: {
    padding: "10px 14px",
    borderRadius: "6px",
    border: "1px solid rgba(255, 255, 255, 0.2)",
    backgroundColor: "rgba(255, 255, 255, 0.08)",
    color: "#e2e8f0",
    fontWeight: 500,
    fontSize: "0.85rem",
    cursor: "pointer",
  },
  tipRow: {
    display: "flex",
    justifyContent: "space-between",
    alignItems: "center",
  },
  hint: {
    fontSize: "0.75rem",
    color: "#718096",
  },
  additionalSection: {
    marginTop: "14px",
    paddingTop: "12px",
    borderTop: "1px dashed rgba(255, 255, 255, 0.15)",
  },
  additionalHeader: {
    marginBottom: "10px",
  },
  additionalTitle: {
    fontSize: "0.82rem",
    fontWeight: 600,
    color: "#90cdf4",
    textTransform: "uppercase",
    letterSpacing: "0.5px",
  },
  additionalRow: {
    display: "flex",
    gap: "10px",
    flexWrap: "wrap",
  },
};

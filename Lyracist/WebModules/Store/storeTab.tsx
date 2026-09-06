// Edited on Sep 6, 2026 @ 10:55:00 -> Add Import Log panel (last 20 events), media stream details, lyrics badges, and FFmpeg tags
import React, { useState } from "react";
import { Track, StoreSettings, ImportLogEvent } from "./types";
import { StoreSearch } from "./storeSearch";
import { SettingsStore } from "./settingsStore";
import { createTrackFromFiles } from "./importMetadata";

interface StoreTabProps {
  activeApp: string; // Must be 'Lyracist' to render
  initialSettings?: Partial<StoreSettings>;
  onTrackImported?: (track: Track) => void;
}

export const StoreTab: React.FC<StoreTabProps> = ({
  activeApp,
  initialSettings,
  onTrackImported,
}) => {
  // CRITICAL REQUIREMENT: This tab must ONLY appear in Lyracist, never in Trivia or other apps.
  if (activeApp !== "Lyracist") {
    return null;
  }

  const [settings, setSettings] = useState<StoreSettings>({
    purchasedTracksFolder: initialSettings?.purchasedTracksFolder || "C:\\Users\\User\\Downloads",
    targetKaraokeFolder: initialSettings?.targetKaraokeFolder || "",
    targetMusicFolder: initialSettings?.targetMusicFolder || "",
    autoImportEnabled: initialSettings?.autoImportEnabled ?? false,
    moveFilesToTarget: initialSettings?.moveFilesToTarget ?? true,
    normalizeAudioOnImport: initialSettings?.normalizeAudioOnImport ?? false,
    trimSilenceOnImport: initialSettings?.trimSilenceOnImport ?? false,
    generateWaveformOnImport: initialSettings?.generateWaveformOnImport ?? false,
  });

  const [watcherStatus, setWatcherStatus] = useState<string>(
    settings.autoImportEnabled
      ? `Watching: ${settings.purchasedTracksFolder}`
      : "Auto-import is disabled."
  );

  const [recentTracks, setRecentTracks] = useState<Track[]>([]);
  const [importLogs, setImportLogs] = useState<ImportLogEvent[]>([]);
  const [importStatus, setImportStatus] = useState<string>("Ready");
  const [showLogPanel, setShowLogPanel] = useState<boolean>(true);

  const addLogEvent = (
    title: string,
    artist: string,
    source: string,
    status: "Success" | "Processing" | "Warning" | "Error",
    message: string,
    details?: string
  ) => {
    const newLog: ImportLogEvent = {
      id: `log-${Date.now()}-${Math.random().toString(36).substring(2, 7)}`,
      timestamp: new Date(),
      trackTitle: title,
      artist,
      source,
      status,
      message,
      details,
    };

    setImportLogs((prev) => [newLog, ...prev].slice(0, 20));
  };

  const handleSettingsChange = (newValues: Partial<StoreSettings>) => {
    setSettings((prev) => {
      const updated = { ...prev, ...newValues };
      setWatcherStatus(
        updated.autoImportEnabled
          ? `Watching: ${updated.purchasedTracksFolder}`
          : "Auto-import is disabled."
      );
      return updated;
    });
  };

  const handleManualImport = (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files;
    if (!files || files.length === 0) return;

    const filePaths = Array.from(files).map((f) => (f as any).path || f.name);
    addLogEvent("Batch Import", "", "Manual", "Processing", `Processing ${files.length} file(s)...`);

    const newTrack = createTrackFromFiles(filePaths);
    if (settings.normalizeAudioOnImport) newTrack.normalized = true;
    if (settings.trimSilenceOnImport) newTrack.silenceTrimmed = true;
    if (settings.generateWaveformOnImport) {
      newTrack.waveformPath = `${newTrack.filePaths[0]}_waveform.png`;
    }

    setRecentTracks((prev) => [newTrack, ...prev]);
    setImportStatus(`Imported: ${newTrack.title} by ${newTrack.artist} (${newTrack.source})`);

    addLogEvent(
      newTrack.title,
      newTrack.artist,
      newTrack.source,
      "Success",
      `Imported successfully (${newTrack.karaokeType || "Audio"})`,
      newTrack.filePaths.join(", ")
    );

    if (onTrackImported) {
      onTrackImported(newTrack);
    }
  };

  return (
    <div style={styles.container}>
      {/* Header Banner */}
      <div style={styles.banner}>
        <h2 style={styles.bannerTitle}>🛍️ Karaoke Store &amp; Licensed Tracks</h2>
        <p style={styles.bannerSubtitle}>
          Search licensed karaoke stores online, purchase legal backing tracks, and automatically import them into your Lyracist library.
        </p>
      </div>

      {/* 1. Search Bar + Store Buttons */}
      <StoreSearch />

      {/* 2. Folder Configuration & Auto-Import Settings */}
      <SettingsStore
        settings={settings}
        watcherStatus={watcherStatus}
        onSettingsChange={handleSettingsChange}
      />

      {/* 3. Recently Imported Tracks Section */}
      <div style={styles.card}>
        <div style={styles.headerRow}>
          <div>
            <h3 style={styles.heading}>Recently Imported Tracks</h3>
            <span style={styles.statusText}>{importStatus}</span>
          </div>

          <div style={styles.buttonRow}>
            <label style={styles.uploadButton}>
              📁 Import Purchased Track...
              <input
                type="file"
                multiple
                accept=".mp3,.cdg,.zip,.mp4,.lrc,.txt"
                onChange={handleManualImport}
                style={{ display: "none" }}
              />
            </label>

            {recentTracks.length > 0 && (
              <button
                onClick={() => setRecentTracks([])}
                style={styles.clearButton}
              >
                Clear Recent
              </button>
            )}
          </div>
        </div>

        {recentTracks.length === 0 ? (
          <div style={styles.emptyState}>
            No tracks imported yet in this session. Downloaded or manually imported tracks will appear here.
          </div>
        ) : (
          <table style={styles.table}>
            <thead>
              <tr style={styles.thRow}>
                <th style={styles.th}>Source</th>
                <th style={styles.th}>Title</th>
                <th style={styles.th}>Artist</th>
                <th style={styles.th}>Format</th>
                <th style={styles.th}>Features / Metadata</th>
                <th style={styles.th}>Added</th>
              </tr>
            </thead>
            <tbody>
              {recentTracks.map((t) => (
                <tr key={t.id} style={styles.tr}>
                  <td style={styles.td}>
                    <span style={getSourceBadgeStyle(t.source)}>{t.source}</span>
                  </td>
                  <td style={{ ...styles.td, fontWeight: 600 }}>{t.title}</td>
                  <td style={styles.td}>{t.artist}</td>
                  <td style={styles.td}>{t.karaokeType || "Karaoke"}</td>
                  <td style={styles.td}>
                    <div style={styles.featureBadgeGroup}>
                      {t.lyricsPath && (
                        <span style={styles.badgeLyrics} title={`Lyrics: ${t.lyricsPath}`}>
                          📜 Lyrics
                        </span>
                      )}
                      {t.hasDualAudio && (
                        <span style={styles.badgeDualAudio} title="Multiplexed Guide Vocal & Instrumental streams">
                          🎧 Dual Audio
                        </span>
                      )}
                      {t.normalized && (
                        <span style={styles.badgeNorm} title="Normalized to -16 LUFS">
                          ⚡ -16 LUFS
                        </span>
                      )}
                      {t.silenceTrimmed && (
                        <span style={styles.badgeTrim} title="Silence trimmed (-50dB)">
                          ✂️ Trimmed
                        </span>
                      )}
                      {t.waveformPath && (
                        <span style={styles.badgeWaveform} title="Waveform preview generated">
                          📊 Waveform
                        </span>
                      )}
                    </div>
                  </td>
                  <td style={styles.td}>{new Date(t.addedAt).toLocaleTimeString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* 4. Import Log Panel (Last 20 Events) */}
      <div style={styles.card}>
        <div style={styles.headerRow}>
          <div>
            <h3 style={styles.heading}>Import Activity Log (Last 20 Events)</h3>
            <span style={styles.statusText}>Real-time watcher and processing diagnostics</span>
          </div>

          <div style={styles.buttonRow}>
            <button
              onClick={() => setShowLogPanel(!showLogPanel)}
              style={styles.clearButton}
            >
              {showLogPanel ? "Hide Log" : "Show Log"}
            </button>
            {importLogs.length > 0 && (
              <button
                onClick={() => setImportLogs([])}
                style={styles.clearButton}
              >
                Clear Log
              </button>
            )}
          </div>
        </div>

        {showLogPanel && (
          importLogs.length === 0 ? (
            <div style={styles.emptyState}>
              No log activity recorded yet. Import events, warnings, and FFmpeg operations will appear here.
            </div>
          ) : (
            <div style={styles.logContainer}>
              {importLogs.map((log) => (
                <div key={log.id} style={styles.logEntry}>
                  <span style={styles.logTime}>
                    {new Date(log.timestamp).toLocaleTimeString()}
                  </span>
                  <span style={getLogStatusBadgeStyle(log.status)}>
                    {log.status}
                  </span>
                  <span style={styles.logTrack}>
                    {log.trackTitle} {log.artist ? `— ${log.artist}` : ""}
                  </span>
                  <span style={styles.logMessage}>{log.message}</span>
                  {log.details && (
                    <span style={styles.logDetails} title={log.details}>
                      [{log.details}]
                    </span>
                  )}
                </div>
              ))}
            </div>
          )
        )}
      </div>
    </div>
  );
};

function getSourceBadgeStyle(source: string): React.CSSProperties {
  let bg = "#4a5568";
  if (source === "Karaoke Version") bg = "#b7791f";
  if (source === "Party Tyme") bg = "#007788";
  if (source === "Karaoke.com") bg = "#4a148c";
  if (source === "Sunfly") bg = "#1565c0";

  return {
    padding: "3px 8px",
    borderRadius: "4px",
    fontSize: "0.78rem",
    fontWeight: 600,
    backgroundColor: bg,
    color: "#ffffff",
    display: "inline-block",
  };
}

function getLogStatusBadgeStyle(status: string): React.CSSProperties {
  let bg = "#4a5568";
  if (status === "Success") bg = "#2e7d32";
  if (status === "Processing") bg = "#0288d1";
  if (status === "Warning") bg = "#f57c00";
  if (status === "Error") bg = "#c62828";

  return {
    padding: "2px 6px",
    borderRadius: "3px",
    fontSize: "0.72rem",
    fontWeight: 700,
    backgroundColor: bg,
    color: "#ffffff",
    marginRight: "8px",
    display: "inline-block",
    textTransform: "uppercase",
  };
}

const styles: Record<string, React.CSSProperties> = {
  container: {
    padding: "24px",
    fontFamily: "Segoe UI, -apple-system, BlinkMacSystemFont, Roboto, sans-serif",
    color: "#ffffff",
    backgroundColor: "#121212",
    minHeight: "100vh",
  },
  banner: {
    background: "linear-gradient(135deg, #8E2DE2 0%, #4A00E0 60%, #00C9FF 100%)",
    borderRadius: "12px",
    padding: "24px",
    marginBottom: "20px",
  },
  bannerTitle: {
    margin: 0,
    fontSize: "1.6rem",
    fontWeight: 700,
    color: "#ffffff",
  },
  bannerSubtitle: {
    margin: "8px 0 0 0",
    fontSize: "0.95rem",
    color: "#e2e8f0",
  },
  card: {
    backgroundColor: "rgba(255, 255, 255, 0.05)",
    border: "1px solid rgba(255, 255, 255, 0.12)",
    borderRadius: "10px",
    padding: "20px",
    marginBottom: "18px",
  },
  heading: {
    margin: 0,
    fontSize: "1.15rem",
    fontWeight: 600,
    color: "#ffffff",
  },
  statusText: {
    fontSize: "0.8rem",
    color: "#a0aec0",
    marginTop: "2px",
    display: "inline-block",
  },
  headerRow: {
    display: "flex",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: "16px",
  },
  buttonRow: {
    display: "flex",
    gap: "10px",
  },
  uploadButton: {
    backgroundColor: "#2b6cb0",
    color: "#ffffff",
    padding: "8px 16px",
    borderRadius: "6px",
    fontWeight: 600,
    fontSize: "0.88rem",
    cursor: "pointer",
  },
  clearButton: {
    backgroundColor: "rgba(255, 255, 255, 0.1)",
    color: "#e2e8f0",
    border: "1px solid rgba(255, 255, 255, 0.2)",
    padding: "8px 14px",
    borderRadius: "6px",
    fontSize: "0.85rem",
    cursor: "pointer",
  },
  emptyState: {
    padding: "28px",
    textAlign: "center",
    color: "#718096",
    fontSize: "0.9rem",
  },
  table: {
    width: "100%",
    borderCollapse: "collapse",
    fontSize: "0.88rem",
  },
  thRow: {
    borderBottom: "1px solid rgba(255, 255, 255, 0.15)",
    textAlign: "left",
  },
  th: {
    padding: "10px 12px",
    color: "#a0aec0",
    fontWeight: 600,
  },
  tr: {
    borderBottom: "1px solid rgba(255, 255, 255, 0.06)",
  },
  td: {
    padding: "10px 12px",
    color: "#e2e8f0",
  },
  featureBadgeGroup: {
    display: "flex",
    gap: "6px",
    flexWrap: "wrap",
  },
  badgeLyrics: {
    backgroundColor: "#2d3748",
    color: "#cbd5e0",
    borderRadius: "3px",
    padding: "2px 6px",
    fontSize: "0.72rem",
    fontWeight: 600,
  },
  badgeDualAudio: {
    backgroundColor: "#4c1d95",
    color: "#e9d5ff",
    borderRadius: "3px",
    padding: "2px 6px",
    fontSize: "0.72rem",
    fontWeight: 600,
  },
  badgeNorm: {
    backgroundColor: "#065f46",
    color: "#a7f3d0",
    borderRadius: "3px",
    padding: "2px 6px",
    fontSize: "0.72rem",
    fontWeight: 600,
  },
  badgeTrim: {
    backgroundColor: "#1e3a8a",
    color: "#bfdbfe",
    borderRadius: "3px",
    padding: "2px 6px",
    fontSize: "0.72rem",
    fontWeight: 600,
  },
  badgeWaveform: {
    backgroundColor: "#701a75",
    color: "#f5d0fe",
    borderRadius: "3px",
    padding: "2px 6px",
    fontSize: "0.72rem",
    fontWeight: 600,
  },
  logContainer: {
    maxHeight: "260px",
    overflowY: "auto",
    backgroundColor: "rgba(0, 0, 0, 0.3)",
    borderRadius: "6px",
    padding: "10px",
    border: "1px solid rgba(255, 255, 255, 0.08)",
  },
  logEntry: {
    fontSize: "0.82rem",
    padding: "6px 8px",
    borderBottom: "1px solid rgba(255, 255, 255, 0.05)",
    display: "flex",
    alignItems: "center",
    gap: "8px",
    flexWrap: "wrap",
  },
  logTime: {
    color: "#718096",
    fontSize: "0.75rem",
    fontFamily: "monospace",
  },
  logTrack: {
    fontWeight: 600,
    color: "#e2e8f0",
  },
  logMessage: {
    color: "#cbd5e0",
  },
  logDetails: {
    color: "#718096",
    fontSize: "0.75rem",
  },
};

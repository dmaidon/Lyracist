// Edited on Sep 6, 2026 @ 10:53:00 -> Add audio normalization, silence trimming, and waveform preview toggles
import React from "react";
import { StoreSettings } from "./types";

interface SettingsStoreProps {
  settings: StoreSettings;
  watcherStatus: string;
  onSettingsChange: (newSettings: Partial<StoreSettings>) => void;
}

export const SettingsStore: React.FC<SettingsStoreProps> = ({
  settings,
  watcherStatus,
  onSettingsChange,
}) => {
  return (
    <div style={styles.card}>
      <h3 style={styles.heading}>Purchased Tracks Folder &amp; Target Routing</h3>
      <p style={styles.subtext}>
        Configure where downloaded files are detected, destination folders, and FFmpeg audio processing settings.
      </p>

      {/* Purchased Tracks Folder */}
      <div style={styles.fieldGroup}>
        <label style={styles.label}>Purchased Tracks Folder (Download Watcher):</label>
        <input
          type="text"
          value={settings.purchasedTracksFolder}
          onChange={(e) => onSettingsChange({ purchasedTracksFolder: e.target.value })}
          placeholder="Path to watch for downloads (e.g. C:\Users\name\Downloads)"
          style={styles.input}
        />
      </div>

      {/* Target Karaoke Folder */}
      <div style={styles.fieldGroup}>
        <label style={styles.label}>Target Karaoke Folder (.zip, .cdg, .mp4):</label>
        <input
          type="text"
          value={settings.targetKaraokeFolder}
          onChange={(e) => onSettingsChange({ targetKaraokeFolder: e.target.value })}
          placeholder="(Optional) Destination folder for imported karaoke files..."
          style={styles.input}
        />
      </div>

      {/* Target Music Folder */}
      <div style={styles.fieldGroup}>
        <label style={styles.label}>Target Music / Audio Folder (standalone .mp3):</label>
        <input
          type="text"
          value={settings.targetMusicFolder}
          onChange={(e) => onSettingsChange({ targetMusicFolder: e.target.value })}
          placeholder="(Optional) Destination folder for background music files..."
          style={styles.input}
        />
      </div>

      {/* Folder Toggles */}
      <div style={styles.toggleRow}>
        <label style={styles.checkboxLabel}>
          <input
            type="checkbox"
            checked={settings.autoImportEnabled}
            onChange={(e) => onSettingsChange({ autoImportEnabled: e.target.checked })}
            style={styles.checkbox}
          />
          Automatically import new files from Purchased Tracks folder
        </label>

        <label style={styles.checkboxLabel}>
          <input
            type="checkbox"
            checked={settings.moveFilesToTarget}
            onChange={(e) => onSettingsChange({ moveFilesToTarget: e.target.checked })}
            style={styles.checkbox}
          />
          Move imported files to destination folders (Karaoke / Music)
        </label>
      </div>

      {/* FFmpeg Processing Pipeline Toggles */}
      <div style={styles.ffmpegSection}>
        <span style={styles.sectionSubtitle}>FFmpeg Import Processing Pipeline:</span>
        <div style={styles.toggleRow}>
          <label style={styles.checkboxLabel}>
            <input
              type="checkbox"
              checked={settings.normalizeAudioOnImport}
              onChange={(e) => onSettingsChange({ normalizeAudioOnImport: e.target.checked })}
              style={styles.checkbox}
            />
            Normalize Audio on Import (EBU R128 -16 LUFS)
          </label>

          <label style={styles.checkboxLabel}>
            <input
              type="checkbox"
              checked={settings.trimSilenceOnImport}
              onChange={(e) => onSettingsChange({ trimSilenceOnImport: e.target.checked })}
              style={styles.checkbox}
            />
            Trim Silence on Import (-50dB lead-in/lead-out)
          </label>

          <label style={styles.checkboxLabel}>
            <input
              type="checkbox"
              checked={settings.generateWaveformOnImport}
              onChange={(e) => onSettingsChange({ generateWaveformOnImport: e.target.checked })}
              style={styles.checkbox}
            />
            Generate Waveform Preview
          </label>
        </div>
      </div>

      {/* Watcher Status Badge */}
      <div style={styles.statusBox}>
        <span style={styles.statusLabel}>Watcher Status:</span>
        <span style={styles.statusValue}>{watcherStatus}</span>
      </div>
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
    margin: "0 0 16px 0",
    fontSize: "0.85rem",
    color: "#a0aec0",
    lineHeight: 1.4,
  },
  fieldGroup: {
    marginBottom: "12px",
  },
  label: {
    display: "block",
    fontSize: "0.85rem",
    fontWeight: 600,
    color: "#e2e8f0",
    marginBottom: "4px",
  },
  input: {
    width: "100%",
    padding: "9px 12px",
    borderRadius: "6px",
    border: "1px solid rgba(255, 255, 255, 0.2)",
    backgroundColor: "rgba(0, 0, 0, 0.3)",
    color: "#ffffff",
    fontSize: "0.9rem",
    boxSizing: "border-box",
    outline: "none",
  },
  toggleRow: {
    display: "flex",
    flexWrap: "wrap",
    gap: "24px",
    margin: "12px 0",
  },
  checkboxLabel: {
    display: "flex",
    alignItems: "center",
    gap: "8px",
    fontSize: "0.88rem",
    color: "#e2e8f0",
    cursor: "pointer",
  },
  checkbox: {
    cursor: "pointer",
  },
  ffmpegSection: {
    marginTop: "12px",
    paddingTop: "12px",
    borderTop: "1px solid rgba(255, 255, 255, 0.1)",
  },
  sectionSubtitle: {
    display: "block",
    fontSize: "0.82rem",
    fontWeight: 600,
    color: "#a0aec0",
    textTransform: "uppercase",
    letterSpacing: "0.5px",
    marginBottom: "4px",
  },
  statusBox: {
    padding: "10px 14px",
    borderRadius: "6px",
    backgroundColor: "rgba(0, 0, 0, 0.25)",
    border: "1px solid rgba(255, 255, 255, 0.08)",
    display: "flex",
    gap: "8px",
    alignItems: "center",
    fontSize: "0.85rem",
    marginTop: "12px",
  },
  statusLabel: {
    fontWeight: 600,
    color: "#ffffff",
  },
  statusValue: {
    color: "#cbd5e0",
  },
};

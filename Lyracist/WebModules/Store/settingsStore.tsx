// Edited on Sep 6, 2026 @ 12:47:00 -> Add Provider Settings panel: preferred provider, file type, audio toggles, target folder, and lyrics format
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

      {/* Provider & Format Preferences Section */}
      <div style={styles.ffmpegSection}>
        <span style={styles.sectionSubtitle}>Provider Preferences &amp; Import Defaults:</span>
        <div style={styles.selectGrid}>
          {/* Preferred Provider */}
          <div style={styles.selectGroup}>
            <label style={styles.label}>Preferred Provider:</label>
            <select
              value={settings.preferredProvider || "KV"}
              onChange={(e) => onSettingsChange({ preferredProvider: e.target.value as any })}
              style={styles.select}
            >
              <option value="KV">Karaoke Version (KV)</option>
              <option value="PT">Party Tyme (PT)</option>
              <option value="Sunfly">Sunfly (SF)</option>
              <option value="Karaoke.com">Karaoke.com</option>
            </select>
          </div>

          {/* Preferred File Type */}
          <div style={styles.selectGroup}>
            <label style={styles.label}>Preferred File Type:</label>
            <select
              value={settings.preferredFileType || "MP3+G"}
              onChange={(e) => onSettingsChange({ preferredFileType: e.target.value as any })}
              style={styles.select}
            >
              <option value="MP3+G">MP3+G (Paired Audio/Graphics)</option>
              <option value="MP4">MP4 Video</option>
              <option value="Audio-only">Audio-only Backing</option>
            </select>
          </div>

          {/* Preferred Target Folder */}
          <div style={styles.selectGroup}>
            <label style={styles.label}>Preferred Target Folder:</label>
            <select
              value={settings.preferredTargetFolder || "Karaoke"}
              onChange={(e) => onSettingsChange({ preferredTargetFolder: e.target.value as any })}
              style={styles.select}
            >
              <option value="Karaoke">Karaoke Folder</option>
              <option value="Music">Music / Audio Folder</option>
            </select>
          </div>

          {/* Preferred Lyrics Format */}
          <div style={styles.selectGroup}>
            <label style={styles.label}>Preferred Lyrics Format:</label>
            <select
              value={settings.preferredLyricsFormat || "LRC"}
              onChange={(e) => onSettingsChange({ preferredLyricsFormat: e.target.value as any })}
              style={styles.select}
            >
              <option value="LRC">.LRC (Synchronized)</option>
              <option value="TXT">.TXT (Plain Lyrics)</option>
            </select>
          </div>
        </div>

        {/* Baseline Audio Processing Toggles */}
        <div style={styles.toggleRow}>
          <label style={styles.checkboxLabel}>
            <input
              type="checkbox"
              checked={settings.defaultNormalizeAudio ?? true}
              onChange={(e) => onSettingsChange({ defaultNormalizeAudio: e.target.checked })}
              style={styles.checkbox}
            />
            Normalize audio by default
          </label>

          <label style={styles.checkboxLabel}>
            <input
              type="checkbox"
              checked={settings.defaultTrimSilence ?? true}
              onChange={(e) => onSettingsChange({ defaultTrimSilence: e.target.checked })}
              style={styles.checkbox}
            />
            Trim silence by default
          </label>

          <label style={styles.checkboxLabel}>
            <input
              type="checkbox"
              checked={settings.defaultGenerateWaveform ?? true}
              onChange={(e) => onSettingsChange({ defaultGenerateWaveform: e.target.checked })}
              style={styles.checkbox}
            />
            Generate waveform by default
          </label>
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", marginTop: "10px" }}>
          <button
            type="button"
            onClick={() => onSettingsChange({ ...settings })}
            style={styles.saveButton}
          >
            ✓ Save Settings
          </button>
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
  selectGrid: {
    display: "grid",
    gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))",
    gap: "14px",
    margin: "12px 0",
  },
  selectGroup: {
    display: "flex",
    flexDirection: "column",
  },
  select: {
    padding: "8px 10px",
    borderRadius: "6px",
    border: "1px solid rgba(255, 255, 255, 0.2)",
    backgroundColor: "rgba(0, 0, 0, 0.4)",
    color: "#ffffff",
    fontSize: "0.85rem",
    outline: "none",
  },
  saveButton: {
    padding: "8px 18px",
    borderRadius: "6px",
    border: "none",
    backgroundColor: "#38bdf8",
    color: "#0f172a",
    fontSize: "0.88rem",
    fontWeight: 600,
    cursor: "pointer",
  },
};

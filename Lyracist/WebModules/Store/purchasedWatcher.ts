// Edited on Sep 6, 2026 @ 11:09:00 -> Integrate advanced provider fingerprinting (ZIP, ID3, CDG, MP4, heuristics, referrer hint)
import * as fs from "fs";
import * as path from "path";
import { Track, TrackSource, StoreSettings, ImportLogEvent } from "./types";
import {
  createTrackFromFiles,
  ITrackDatabase,
  parseTrackMetadata,
  inspectZipForProvider,
  detectProviderFromCdgHeader,
  detectProviderFromId3,
  detectProviderFromMp4,
  getReferrerHint,
} from "./importMetadata";
import { probeMediaFile, normalizeAudio, trimSilence, generateWaveformPreview } from "./ffmpegUtils";

export type OnTrackImportedCallback = (track: Track) => void;
export type OnStatusChangedCallback = (status: string) => void;
export type OnImportLogCallback = (event: ImportLogEvent) => void;

export class PurchasedTrackWatcher {
  private settings: StoreSettings;
  private db: ITrackDatabase;
  private fsWatcher: fs.FSWatcher | null = null;
  private isWatching = false;
  private recentlyProcessed: Map<string, number> = new Map();
  private onTrackImported?: OnTrackImportedCallback;
  private onStatusChanged?: OnStatusChangedCallback;
  private onImportLog?: OnImportLogCallback;

  private supportedExtensions = new Set([".mp3", ".cdg", ".zip", ".mp4"]);

  constructor(
    settings: StoreSettings,
    db: ITrackDatabase,
    callbacks?: {
      onTrackImported?: OnTrackImportedCallback;
      onStatusChanged?: OnStatusChangedCallback;
      onImportLog?: OnImportLogCallback;
    }
  ) {
    this.settings = { ...settings };
    this.db = db;
    if (callbacks) {
      this.onTrackImported = callbacks.onTrackImported;
      this.onStatusChanged = callbacks.onStatusChanged;
      this.onImportLog = callbacks.onImportLog;
    }
  }

  public updateSettings(newSettings: Partial<StoreSettings>): void {
    this.settings = { ...this.settings, ...newSettings };
    if (this.isWatching) {
      this.stop();
      this.start();
    }
  }

  public start(): void {
    if (this.isWatching) return;

    if (!this.settings.autoImportEnabled) {
      if (this.onStatusChanged) {
        this.onStatusChanged("Auto-import is disabled.");
      }
      return;
    }

    const folder = this.settings.purchasedTracksFolder;
    if (!folder || !fs.existsSync(folder)) {
      if (this.onStatusChanged) {
        this.onStatusChanged(`Folder does not exist: ${folder}`);
      }
      return;
    }

    try {
      this.fsWatcher = fs.watch(folder, (eventType, filename) => {
        if (!filename) return;
        const fullPath = path.join(folder, filename);
        if (eventType === "rename") {
          this.handleFileDetected(fullPath);
        }
      });

      this.isWatching = true;
      if (this.onStatusChanged) {
        this.onStatusChanged(`Monitoring: ${folder}`);
      }
    } catch (err: any) {
      if (this.onStatusChanged) {
        this.onStatusChanged(`Error starting watcher: ${err.message}`);
      }
    }
  }

  public stop(): void {
    if (this.fsWatcher) {
      this.fsWatcher.close();
      this.fsWatcher = null;
    }
    this.isWatching = false;
    if (this.onStatusChanged) {
      this.onStatusChanged("Watcher stopped.");
    }
  }

  private logEvent(
    trackTitle: string,
    artist: string,
    source: string,
    status: ImportLogEvent["status"],
    message: string,
    details?: string
  ): void {
    if (this.onImportLog) {
      this.onImportLog({
        id: `log-${Date.now()}-${Math.random().toString(36).substring(2, 7)}`,
        timestamp: new Date(),
        trackTitle,
        artist,
        source,
        status,
        message,
        details,
      });
    }
  }

  private handleFileDetected(filePath: string): void {
    const ext = path.extname(filePath).toLowerCase();
    if (!this.supportedExtensions.has(ext)) return;

    const basename = path.basename(filePath);
    if (basename.endsWith(".crdownload") || basename.endsWith(".part") || basename.endsWith(".tmp") || basename.startsWith("~$")) {
      return;
    }

    const now = Date.now();
    const last = this.recentlyProcessed.get(filePath) || 0;
    if (now - last < 5000) return;
    this.recentlyProcessed.set(filePath, now);

    setTimeout(async () => {
      await this.importFile(filePath);
    }, 2000);
  }

  public async importFile(filePath: string): Promise<Track | null> {
    if (!fs.existsSync(filePath)) return null;

    const ext = path.extname(filePath).toLowerCase();
    if (!this.supportedExtensions.has(ext)) return null;

    const meta = parseTrackMetadata(filePath);
    this.logEvent(meta.title, meta.artist, meta.source, "Processing", `Starting import for ${path.basename(filePath)}`);

    let fileGroup: string[] = [filePath];

    if (ext === ".mp3") {
      const partnerCdg = filePath.replace(/\.mp3$/i, ".cdg");
      for (let i = 0; i < 8; i++) {
        if (fs.existsSync(partnerCdg)) {
          fileGroup.push(partnerCdg);
          this.recentlyProcessed.set(partnerCdg, Date.now());
          break;
        }
        await new Promise((res) => setTimeout(res, 500));
      }
    } else if (ext === ".cdg") {
      const partnerMp3 = filePath.replace(/\.cdg$/i, ".mp3");
      for (let i = 0; i < 8; i++) {
        if (fs.existsSync(partnerMp3)) {
          fileGroup.unshift(partnerMp3);
          this.recentlyProcessed.set(partnerMp3, Date.now());
          break;
        }
        await new Promise((res) => setTimeout(res, 500));
      }
    }

    const baseWithoutExt = filePath.substring(0, filePath.lastIndexOf("."));
    const lrcPath = `${baseWithoutExt}.lrc`;
    const txtPath = `${baseWithoutExt}.txt`;
    if (fs.existsSync(lrcPath) && !fileGroup.includes(lrcPath)) {
      fileGroup.push(lrcPath);
    } else if (fs.existsSync(txtPath) && !fileGroup.includes(txtPath)) {
      fileGroup.push(txtPath);
    }

    const probeInfo = await probeMediaFile(fileGroup[0]);

    // Advanced Provider Fingerprinting ("Provider Intelligence")
    let detectedSource: TrackSource | null = null;

    if (ext === ".zip") {
      detectedSource = inspectZipForProvider(filePath);
    }

    if (!detectedSource) {
      const cdgFile = fileGroup.find((f) => f.toLowerCase().endsWith(".cdg"));
      if (cdgFile) {
        detectedSource = detectProviderFromCdgHeader(cdgFile);
      }
    }

    if (!detectedSource && (ext === ".mp3" || fileGroup.some((f) => f.toLowerCase().endsWith(".mp3")))) {
      detectedSource = detectProviderFromId3(probeInfo);
    }

    if (!detectedSource && ext === ".mp4") {
      detectedSource = detectProviderFromMp4(probeInfo);
    }

    if (!detectedSource && meta.source !== "Local") {
      detectedSource = meta.source;
    }

    if (!detectedSource) {
      detectedSource = getReferrerHint();
    }

    const finalSource: TrackSource = detectedSource || "Local";

    let finalPaths = fileGroup;
    if (this.settings.moveFilesToTarget) {
      const isKaraoke = meta.isKaraoke || fileGroup.some((f) => f.endsWith(".cdg") || f.endsWith(".zip"));
      const targetFolder = isKaraoke
        ? this.settings.targetKaraokeFolder
        : this.settings.targetMusicFolder;

      if (targetFolder && fs.existsSync(targetFolder)) {
        finalPaths = this.moveFiles(fileGroup, targetFolder);
      }
    }

    const primaryTarget = finalPaths[0];

    let normalized = false;
    let silenceTrimmed = false;
    let waveformPath: string | undefined;

    const isAudioOnly = primaryTarget.endsWith(".mp3") && !primaryTarget.endsWith(".zip");

    if (isAudioOnly && this.settings.normalizeAudioOnImport) {
      try {
        await normalizeAudio(primaryTarget);
        normalized = true;
      } catch (err: any) {
        this.logEvent(meta.title, meta.artist, finalSource, "Warning", `Loudness normalization failed: ${err.message}`);
      }
    }

    if (isAudioOnly && this.settings.trimSilenceOnImport) {
      try {
        await trimSilence(primaryTarget);
        silenceTrimmed = true;
      } catch (err: any) {
        this.logEvent(meta.title, meta.artist, finalSource, "Warning", `Silence trimming failed: ${err.message}`);
      }
    }

    if (this.settings.generateWaveformOnImport) {
      try {
        waveformPath = await generateWaveformPreview(primaryTarget);
      } catch (err: any) {
        this.logEvent(meta.title, meta.artist, finalSource, "Warning", `Waveform generation failed: ${err.message}`);
      }
    }

    const track = createTrackFromFiles(finalPaths, probeInfo);
    track.source = finalSource;
    track.normalized = normalized;
    track.silenceTrimmed = silenceTrimmed;
    track.waveformPath = waveformPath;

    await this.db.insertTrack(track);

    const dualAudioMsg = probeInfo.hasDualAudio ? " [Dual Audio Streams Detected]" : "";
    this.logEvent(
      track.title,
      track.artist,
      track.source,
      "Success",
      `Imported successfully (${track.karaokeType || "Audio"})${dualAudioMsg}`,
      `File: ${path.basename(primaryTarget)}`
    );

    if (this.onTrackImported) {
      this.onTrackImported(track);
    }

    return track;
  }

  private moveFiles(sourcePaths: string[], targetDir: string): string[] {
    const newPaths: string[] = [];
    for (const src of sourcePaths) {
      try {
        const dest = path.join(targetDir, path.basename(src));
        fs.renameSync(src, dest);
        newPaths.push(dest);
      } catch {
        newPaths.push(src);
      }
    }
    return newPaths;
  }
}

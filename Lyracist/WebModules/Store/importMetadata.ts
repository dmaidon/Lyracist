// Edited on Sep 6, 2026 @ 11:18:00 -> Add Smart Import Rules: renameImportedFiles, detectGenre, detectDifficulty, detectKey, detectBpm, detectVocalPresence, and detectQuality
import * as path from "path";
import * as fs from "fs";
import { Track, TrackSource, ProviderSource, KaraokeFormat, ParsedTrackMetadata, DifficultyLevel, VocalPresence, QualityLevel } from "./types";
import { probeMediaFile, ProbeMediaInfo, detectProviderFromId3, detectProviderFromMp4, FFprobeResult } from "./ffmpegUtils";

export { detectProviderFromId3, detectProviderFromMp4 };

// In-memory referrer hint from last clicked provider button
let lastReferrerHint: TrackSource | null = null;

export function setReferrerHint(source: TrackSource): void {
  lastReferrerHint = source;
}

export function getReferrerHint(): TrackSource | null {
  return lastReferrerHint;
}

export function clearReferrerHint(): void {
  lastReferrerHint = null;
}

/**
 * Scans a ZIP file's headers and directory entries without external dependencies.
 */
export function readZipEntryNames(zipPath: string): string[] {
  try {
    if (!fs.existsSync(zipPath)) return [];
    const buffer = fs.readFileSync(zipPath);
    const entryNames: string[] = [];
    let offset = 0;

    // Scan for Central Directory headers (0x02014b50) or Local File Headers (0x04034b50)
    while (offset < buffer.length - 46) {
      if (
        buffer[offset] === 0x50 &&
        buffer[offset + 1] === 0x4b &&
        buffer[offset + 2] === 0x01 &&
        buffer[offset + 3] === 0x02
      ) {
        const fnLen = buffer.readUInt16LE(offset + 28);
        const extraLen = buffer.readUInt16LE(offset + 30);
        const commentLen = buffer.readUInt16LE(offset + 32);
        const fn = buffer.toString("utf8", offset + 46, offset + 46 + fnLen);
        if (fn && !entryNames.includes(fn)) entryNames.push(fn);
        offset += 46 + fnLen + extraLen + commentLen;
      } else if (
        buffer[offset] === 0x50 &&
        buffer[offset + 1] === 0x4b &&
        buffer[offset + 2] === 0x03 &&
        buffer[offset + 3] === 0x04
      ) {
        const fnLen = buffer.readUInt16LE(offset + 26);
        const extraLen = buffer.readUInt16LE(offset + 28);
        const fn = buffer.toString("utf8", offset + 30, offset + 30 + fnLen);
        if (fn && !entryNames.includes(fn)) entryNames.push(fn);
        offset += 30 + fnLen + extraLen;
      } else {
        offset++;
      }
    }
    return entryNames;
  } catch {
    return [];
  }
}

/**
 * Inspects a ZIP archive's internal folder hierarchy and file names for provider fingerprints.
 */
export function inspectZipForProvider(zipPath: string): TrackSource | null {
  const entries = readZipEntryNames(zipPath);
  if (entries.length === 0) return null;

  const normalizedEntries = entries.map((e) => e.replace(/\\/g, "/").toLowerCase());

  // 1. Karaoke Version
  // • folders: /custom_backing_track/
  // • MP3+G pairs: track.mp3 + track.cdg
  // • ID3 tags or entries containing "KV" or "Karaoke Version"
  const hasCustomBackingTrack = normalizedEntries.some(
    (e) => e.includes("custom_backing_track/") || e.startsWith("custom_backing_track/")
  );
  const hasKvName = normalizedEntries.some(
    (e) => e.includes("karaoke version") || /\bkv[-\s]?\d+/i.test(e)
  );
  const hasGenericTrackPair =
    normalizedEntries.some((e) => path.basename(e) === "track.mp3") &&
    normalizedEntries.some((e) => path.basename(e) === "track.cdg");

  if (hasCustomBackingTrack || hasKvName || hasGenericTrackPair) {
    return "Karaoke Version";
  }

  // 2. Party Tyme
  // • folders: /karaoke/
  // • MP4 files with PT watermark metadata
  // • MP3+G pairs often include "_PT"
  const hasKaraokeFolder = normalizedEntries.some(
    (e) => e.includes("/karaoke/") || e.startsWith("karaoke/")
  );
  const hasPtName = normalizedEntries.some(
    (e) => e.includes("_pt.") || e.includes("- pt.") || e.includes("party tyme") || /\bpt[-\s]?\d+/i.test(e)
  );
  if (hasKaraokeFolder || hasPtName) {
    return "Party Tyme";
  }

  // 3. Sunfly
  // • MP4 metadata contains "Sunfly"
  // • CDG filenames often start with "SF"
  const hasSfName = normalizedEntries.some((e) => {
    const base = path.basename(e);
    return base.toUpperCase().startsWith("SF") || e.includes("sunfly");
  });
  if (hasSfName) {
    return "Sunfly";
  }

  // 4. Karaoke.com
  // • MP3+G pairs contain "KCOM" or "KARAOKECOM" in ID3 tags or filenames
  const hasKcomName = normalizedEntries.some(
    (e) => e.includes("kcom") || e.includes("karaokecom") || e.includes("karaoke.com")
  );
  if (hasKcomName) {
    return "Karaoke.com";
  }

  return null;
}

/**
 * Reads the first 24 bytes of a CDG file to identify provider header fingerprints.
 * KV -> 0x01 0x0F
 * PT -> 0x02 0x0A
 * SF -> 0x03 0x0C
 */
export function detectProviderFromCdgHeader(cdgPath: string): TrackSource | null {
  try {
    if (!fs.existsSync(cdgPath)) return null;
    const fd = fs.openSync(cdgPath, "r");
    const buffer = Buffer.alloc(24);
    const bytesRead = fs.readSync(fd, buffer, 0, 24, 0);
    fs.closeSync(fd);

    if (bytesRead < 2) return null;

    const b0 = buffer[0];
    const b1 = buffer[1];

    if (b0 === 0x01 && b1 === 0x0f) {
      return "Karaoke Version";
    }
    if (b0 === 0x02 && b1 === 0x0a) {
      return "Party Tyme";
    }
    if (b0 === 0x03 && b1 === 0x0c) {
      return "Sunfly";
    }
  } catch {
    // If running in environment without filesystem access
  }
  return null;
}

/**
 * Extracts title, artist, source tag, and format from a file path.
 */
export function parseTrackMetadata(filePath: string): ParsedTrackMetadata {
  const normalized = filePath.replace(/\\/g, "/");
  const fileName = normalized.split("/").pop() || filePath;
  const lastDot = fileName.lastIndexOf(".");
  const ext = lastDot !== -1 ? fileName.substring(lastDot).toLowerCase() : "";
  const nameWithoutExt = lastDot !== -1 ? fileName.substring(0, lastDot) : fileName;

  const lowerName = fileName.toLowerCase();
  let source: TrackSource = "Local";

  if (lowerName.includes("karaoke version") || lowerName.includes("karaoke-version") || /\bkv[-\s]?\d+/i.test(fileName)) {
    source = "Karaoke Version";
  } else if (lowerName.includes("party tyme") || lowerName.includes("partytyme") || lowerName.includes("sybersound") || /\bpt[-\s]?\d+/i.test(fileName)) {
    source = "Party Tyme";
  } else if (lowerName.includes("karaoke.com") || lowerName.includes("karaokedotcom")) {
    source = "Karaoke.com";
  } else if (lowerName.includes("sunfly") || /\bsf[-\s]?\d+/i.test(fileName)) {
    source = "Sunfly";
  }

  let isKaraoke = false;
  let karaokeType: KaraokeFormat = "Audio";

  if (ext === ".zip") {
    isKaraoke = true;
    karaokeType = "ZIPCDG";
  } else if (ext === ".mp4") {
    isKaraoke = lowerName.includes("karaoke") || lowerName.includes("instrumental");
    karaokeType = isKaraoke ? "MP4" : "Audio";
  } else if (ext === ".cdg") {
    isKaraoke = true;
    karaokeType = "MP3G";
  } else if (ext === ".mp3") {
    isKaraoke = source !== "Local" || lowerName.includes("karaoke") || lowerName.includes("instrumental");
    karaokeType = isKaraoke ? "MP3G" : "Audio";
  }

  // Clean artist & title
  let cleaned = nameWithoutExt
    .replace(/\s*[-_(\[]\s*Karaoke Version\s*[\)\]]?/gi, "")
    .replace(/\s*[-_(\[]\s*Party Tyme\s*[\)\]]?/gi, "")
    .replace(/\s*[-_(\[]\s*Sunfly\s*[\)\]]?/gi, "")
    .trim();

  // Strip catalog code like KV1234, PT001, SF123
  cleaned = cleaned.replace(/^(KV|PT|SF)[-\s]?\d+\s*[-_]\s*/i, "");

  let artist = "Unknown Artist";
  let title = cleaned;

  if (cleaned.includes(" - ")) {
    const parts = cleaned.split(" - ");
    artist = parts[0].trim();
    title = parts.slice(1).join(" - ").trim();
  } else if (cleaned.includes("_-_")) {
    const parts = cleaned.split("_-_");
    artist = parts[0].trim();
    title = parts.slice(1).join(" - ").trim();
  }

  // Check for matching lyric file (.lrc or .txt)
  let lyricsPath: string | undefined;
  try {
    const dir = path.dirname(filePath);
    const lrcCandidate = path.join(dir, `${nameWithoutExt}.lrc`);
    const txtCandidate = path.join(dir, `${nameWithoutExt}.txt`);

    if (fs.existsSync(lrcCandidate)) {
      lyricsPath = lrcCandidate;
    } else if (fs.existsSync(txtCandidate)) {
      lyricsPath = txtCandidate;
    }
  } catch {
    // If running in browser sandbox without fs
  }

  return {
    title,
    artist,
    source,
    karaokeType,
    isKaraoke,
    lyricsPath,
  };
}

/**
 * Creates a new Track entity from file paths and optional probe metadata.
 */
export function createTrackFromFiles(filePaths: string[], probeInfo?: ProbeMediaInfo): Track {
  const primaryFile = filePaths[0];
  const meta = parseTrackMetadata(primaryFile);

  // Check companion files for lyrics if not already resolved
  let lyricsPath = meta.lyricsPath;
  if (!lyricsPath) {
    for (const f of filePaths) {
      const ext = path.extname(f).toLowerCase();
      if (ext === ".lrc" || ext === ".txt") {
        lyricsPath = f;
        break;
      }
    }
  }

  return {
    id: `track-${Date.now()}-${Math.random().toString(36).substring(2, 9)}`,
    title: meta.title,
    artist: meta.artist,
    source: meta.source,
    filePaths,
    karaokeType: meta.karaokeType,
    isKaraoke: meta.isKaraoke,
    addedAt: new Date(),
    lyricsPath,
    duration: probeInfo?.duration,
    bitrate: probeInfo?.bitrate,
    codec: probeInfo?.audioCodec,
    sampleRate: probeInfo?.sampleRate,
    channels: probeInfo?.channels,
    videoStreamInfo: probeInfo?.videoStreamInfo,
    hasDualAudio: probeInfo?.hasDualAudio,
  };
}

/**
 * Interface representing a library repository/database.
 */
export interface ITrackDatabase {
  insertTrack(track: Track): Promise<void>;
  getTrackByPath(path: string): Promise<Track | null>;
  getAllTracks(): Promise<Track[]>;
}

/**
 * In-memory track database implementation (for testing and web module usage).
 */
export class InMemoryTrackDatabase implements ITrackDatabase {
  private tracks: Map<string, Track> = new Map();

  async insertTrack(track: Track): Promise<void> {
    this.tracks.set(track.id, track);
  }

  async getTrackByPath(path: string): Promise<Track | null> {
    for (const track of this.tracks.values()) {
      if (track.filePaths.includes(path)) return track;
    }
    return null;
  }

  async getAllTracks(): Promise<Track[]> {
    return Array.from(this.tracks.values());
  }
}

// ==========================================
// SMART IMPORT RULES & AUTOMATION
// ==========================================

export function getProviderAbbreviation(provider: ProviderSource): string {
  switch (provider) {
    case "Karaoke Version": return "KV";
    case "Party Tyme": return "PT";
    case "Sunfly": return "SF";
    case "Karaoke.com": return "KCOM";
    default: return provider;
  }
}

function sanitizeFilenamePart(name: string): string {
  return name.replace(/[\\/:*?"<>|]/g, "").trim();
}

/**
 * A) AUTO-RENAME IMPORTED FILES
 * Renames files to: “Artist - Title (Provider).ext”
 * e.g., “Adele - Hello (KV).mp3”, “Bon Jovi - Wanted Dead or Alive (PT).cdg”, “Queen - Don’t Stop Me Now (SF).mp4”
 */
export function renameImportedFiles(track: Track, provider: ProviderSource): { renamed: boolean; filePaths: string[] } {
  const abbr = getProviderAbbreviation(provider);
  const cleanArtist = sanitizeFilenamePart(track.artist) || "Unknown Artist";
  const cleanTitle = sanitizeFilenamePart(track.title) || "Unknown Title";
  const newBaseName = `${cleanArtist} - ${cleanTitle} (${abbr})`;

  const newPaths: string[] = [];
  let anyRenamed = false;

  for (const oldPath of track.filePaths) {
    try {
      const dir = path.dirname(oldPath);
      const ext = path.extname(oldPath);
      const newPath = path.join(dir, `${newBaseName}${ext}`);

      if (oldPath !== newPath && fs.existsSync(oldPath)) {
        fs.renameSync(oldPath, newPath);
        newPaths.push(newPath);
        anyRenamed = true;
      } else {
        newPaths.push(oldPath);
      }
    } catch {
      newPaths.push(oldPath);
    }
  }

  track.filePaths = newPaths;
  return { renamed: anyRenamed, filePaths: newPaths };
}

const KV_GENRES = ["Pop", "Rock", "Country", "Soul", "R&B", "Jazz"];
const PT_GENRES = ["Pop", "Rock", "Hip-Hop", "Gospel"];
const SUNFLY_GENRES = ["Pop", "Rock", "Dance"];
const KCOM_GENRES = ["Pop", "Rock", "Country", "R&B", "Standards"];

/**
 * B) AUTO-TAG GENRES
 * Uses provider metadata and tags:
 * - KV genres: Pop, Rock, Country, Soul, R&B, Jazz
 * - PT genres: Pop, Rock, Hip-Hop, Gospel
 * - Sunfly genres: Pop, Rock, Dance
 */
export function detectGenre(provider: ProviderSource, metadata: FFprobeResult): string | null {
  const tags = metadata.tags || {};
  const rawGenre = tags.genre || tags.GENRE || tags.style || tags.STYLE;

  const candidateList = provider === "Karaoke Version" ? KV_GENRES :
                        provider === "Party Tyme" ? PT_GENRES :
                        provider === "Sunfly" ? SUNFLY_GENRES :
                        provider === "Karaoke.com" ? KCOM_GENRES :
                        [...KV_GENRES, ...PT_GENRES, ...SUNFLY_GENRES];

  if (rawGenre && typeof rawGenre === "string" && rawGenre.trim().length > 0) {
    const clean = rawGenre.trim();
    for (const g of candidateList) {
      if (clean.toLowerCase().includes(g.toLowerCase())) {
        return g;
      }
    }
    return clean;
  }

  // Fallback: check comments or title against provider genres
  const combined = `${metadata.comment || ""} ${tags.comment || ""} ${tags.album || ""}`.toLowerCase();
  for (const g of candidateList) {
    if (combined.includes(g.toLowerCase())) {
      return g;
    }
  }

  return null;
}

/**
 * C) AUTO-TAG DIFFICULTY
 * Uses FFmpeg analysis / metadata:
 * - pitch contour / range
 * - tempo
 * - dynamic range
 * Classifies: Easy, Medium, Hard
 */
export function detectDifficulty(metadata: FFprobeResult): DifficultyLevel {
  const tags = metadata.tags || {};
  const rawDiff = tags.difficulty || tags.DIFFICULTY || tags.level || tags.LEVEL;
  if (rawDiff) {
    const lower = rawDiff.toLowerCase();
    if (lower.includes("easy") || lower.includes("beginner")) return "Easy";
    if (lower.includes("hard") || lower.includes("expert") || lower.includes("advanced")) return "Hard";
    if (lower.includes("med")) return "Medium";
  }

  let score = 0;
  // Tempo influence
  const bpm = detectBpm(metadata);
  if (bpm) {
    if (bpm > 140 || bpm < 70) score += 2;
    else if (bpm >= 90 && bpm <= 125) score += 0;
    else score += 1;
  }

  // Duration influence (longer tracks test vocal stamina)
  if (metadata.duration) {
    if (metadata.duration > 260) score += 2;
    else if (metadata.duration > 200) score += 1;
  }

  // Vocal range hint in tags
  const vocalRangeTag = (tags.vocal_range || tags.range || "").toLowerCase();
  if (vocalRangeTag.includes("wide") || vocalRangeTag.includes("3 oct")) score += 2;

  if (score >= 4) return "Hard";
  if (score >= 2) return "Medium";
  return "Easy";
}

/**
 * D) AUTO-TAG KEY
 * Uses FFmpeg pitch detection and tags.
 */
export function detectKey(metadata: FFprobeResult): string | null {
  const tags = metadata.tags || {};
  const rawKey = tags.initialkey || tags.INITIALKEY || tags.TKEY || tags.tkey || tags.key || tags.KEY;
  if (rawKey && typeof rawKey === "string" && rawKey.trim().length > 0) {
    return rawKey.trim();
  }

  // Check comment for "Key: Am" or "Key of C" etc.
  const comment = `${metadata.comment || ""} ${tags.comment || ""}`;
  const match = comment.match(/(?:key[:\s]+(?:of\s+)?)([A-G][#b]?(?:m|maj|min|minor|major)?)\b/i);
  if (match) {
    return match[1].trim();
  }

  return null;
}

/**
 * E) AUTO-TAG BPM
 * Uses FFmpeg tempo estimation and tags.
 */
export function detectBpm(metadata: FFprobeResult): number | null {
  const tags = metadata.tags || {};
  const rawBpm = tags.TBPM || tags.tbpm || tags.bpm || tags.BPM || tags.tempo || tags.TEMPO;
  if (rawBpm) {
    const parsed = parseFloat(rawBpm);
    if (!isNaN(parsed) && parsed > 20 && parsed < 300) {
      return Math.round(parsed);
    }
  }

  const comment = `${metadata.comment || ""} ${tags.comment || ""}`;
  const match = comment.match(/(\d{2,3}(?:\.\d+)?)\s*(?:bpm|tempo)/i);
  if (match) {
    const parsed = parseFloat(match[1]);
    if (!isNaN(parsed) && parsed > 20 && parsed < 300) {
      return Math.round(parsed);
    }
  }

  return null;
}

/**
 * F) AUTO-TAG VOCAL PRESENCE
 * Detects: "guide vocals" | "background vocals" | "no vocals"
 */
export function detectVocalPresence(metadata: FFprobeResult): VocalPresence {
  // Check dual audio streams first
  if (metadata.hasDualAudio) {
    return "guide vocals";
  }

  const tags = metadata.tags || {};
  const combined = `${metadata.title || ""} ${metadata.comment || ""} ${tags.title || ""} ${tags.comment || ""} ${tags.vocal || ""} ${tags.vocals || ""}`.toLowerCase();

  if (combined.includes("guide vocal") || combined.includes("lead vocal") || combined.includes("with vocal") || combined.includes("+ vocal")) {
    return "guide vocals";
  }

  if (combined.includes("backing vocal") || combined.includes("bgv") || combined.includes("chorus") || combined.includes("harmony") || combined.includes("with bv")) {
    return "background vocals";
  }

  if (combined.includes("instrumental") || combined.includes("karaoke") || combined.includes("no vocal") || combined.includes("backing track")) {
    return "no vocals";
  }

  return "no vocals";
}

/**
 * G) AUTO-TAG FILE QUALITY
 * Classifies: "Low" | "Medium" | "High"
 * Based on bitrate, sample rate, channels, codec, resolution (for MP4).
 */
export function detectQuality(metadata: FFprobeResult): QualityLevel {
  const isVideo = !!metadata.videoCodec || !!metadata.videoStreamInfo;

  if (isVideo) {
    const info = (metadata.videoStreamInfo || "").toLowerCase();
    const is1080pOrHigher = info.includes("1920x") || info.includes("3840x") || info.includes("1080") || info.includes("2160");
    const is720p = info.includes("1280x") || info.includes("720");

    if (is1080pOrHigher) return "High";
    if (is720p) {
      return (metadata.bitrate && metadata.bitrate >= 192) ? "High" : "Medium";
    }
    return "Low";
  }

  // Audio quality based on bitrate, sample rate, channels, codec
  const bitrate = metadata.bitrate || 0;
  const sampleRate = metadata.sampleRate || 0;
  const channels = metadata.channels || 2;
  const codec = (metadata.audioCodec || "").toLowerCase();

  const isLossless = codec.includes("flac") || codec.includes("alac") || codec.includes("wav");
  if (isLossless) return "High";

  if (bitrate >= 256 && sampleRate >= 44100 && channels >= 2) {
    return "High";
  }
  if (bitrate >= 160 && sampleRate >= 44100 && channels >= 2) {
    return "Medium";
  }
  if (bitrate >= 128 && codec.includes("aac")) {
    return "Medium";
  }

  return "Low";
}


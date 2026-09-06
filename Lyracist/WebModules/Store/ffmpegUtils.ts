// Edited on Sep 6, 2026 @ 11:07:00 -> Add FFprobeResult tag extraction, detectProviderFromId3, and detectProviderFromMp4
import * as path from "path";
import * as fs from "fs";
import { execFile } from "child_process";
import { TrackSource } from "./types";

export interface ProbeMediaInfo {
  duration?: number;
  bitrate?: number;
  audioCodec?: string;
  sampleRate?: number;
  channels?: number;
  audioStreamCount: number;
  hasDualAudio: boolean;
  videoStreamInfo?: string;
  tags?: Record<string, string>;
  title?: string;
  artist?: string;
  comment?: string;
}

export type FFprobeResult = ProbeMediaInfo;

/**
 * Runs ffprobe on a media file to extract technical stream metadata and tags.
 */
export async function probeMediaFile(filePath: string): Promise<ProbeMediaInfo> {
  return new Promise((resolve) => {
    const args = [
      "-v", "quiet",
      "-print_format", "json",
      "-show_format",
      "-show_streams",
      filePath,
    ];

    execFile("ffprobe", args, (err, stdout) => {
      if (err || !stdout) {
        // Fallback default if ffprobe is not installed or errors
        resolve({ audioStreamCount: 1, hasDualAudio: false });
        return;
      }

      try {
        const data = JSON.parse(stdout);
        const format = data.format || {};
        const streams: any[] = data.streams || [];

        const audioStreams = streams.filter((s) => s.codec_type === "audio");
        const videoStreams = streams.filter((s) => s.codec_type === "video");

        const primaryAudio = audioStreams[0];
        const primaryVideo = videoStreams[0];

        const duration = format.duration ? parseFloat(format.duration) : primaryAudio?.duration ? parseFloat(primaryAudio.duration) : undefined;
        const bitrate = format.bit_rate ? Math.round(parseInt(format.bit_rate, 10) / 1000) : undefined;
        const audioCodec = primaryAudio?.codec_name;
        const sampleRate = primaryAudio?.sample_rate ? parseInt(primaryAudio.sample_rate, 10) : undefined;
        const channels = primaryAudio?.channels;

        let videoStreamInfo: string | undefined;
        if (primaryVideo) {
          const w = primaryVideo.width;
          const h = primaryVideo.height;
          const fps = primaryVideo.r_frame_rate || primaryVideo.avg_frame_rate;
          videoStreamInfo = `${w}x${h} (${primaryVideo.codec_name}${fps ? `, ${fps}fps` : ""})`;
        }

        const audioStreamCount = audioStreams.length;
        const hasDualAudio = audioStreamCount >= 2;

        const tags: Record<string, string> = {};
        if (format.tags && typeof format.tags === "object") {
          Object.assign(tags, format.tags);
        }
        for (const s of streams) {
          if (s.tags && typeof s.tags === "object") {
            Object.assign(tags, s.tags);
          }
        }

        const title = tags.title || tags.TITLE;
        const artist = tags.artist || tags.ARTIST;
        const comment = tags.comment || tags.COMMENT || tags.description || tags.DESCRIPTION;

        resolve({
          duration,
          bitrate,
          audioCodec,
          sampleRate,
          channels,
          audioStreamCount,
          hasDualAudio,
          videoStreamInfo,
          tags,
          title,
          artist,
          comment,
        });
      } catch {
        resolve({ audioStreamCount: 1, hasDualAudio: false });
      }
    });
  });
}

/**
 * Detects provider from MP3 ID3 tags (e.g. TXXX:KV, TXXX:PT, TXXX:SF, TXXX:KCOM).
 */
export function detectProviderFromId3(metadata: FFprobeResult): TrackSource | null {
  if (!metadata || !metadata.tags) return null;
  const tags = metadata.tags;

  for (const [rawKey, rawVal] of Object.entries(tags)) {
    const key = rawKey.toUpperCase();
    const val = (rawVal || "").toUpperCase();

    // Karaoke Version: TXXX:KV, KV in key, or value containing "Karaoke Version"
    if (key.includes("TXXX:KV") || key === "KV" || val.includes("KARAOKE VERSION") || val === "KV") {
      return "Karaoke Version";
    }

    // Party Tyme: TXXX:PT, PT in key, or value containing "Party Tyme" / "Sybersound"
    if (key.includes("TXXX:PT") || key === "PT" || val.includes("PARTY TYME") || val === "PT" || val.includes("SYBERSOUND")) {
      return "Party Tyme";
    }

    // Sunfly: TXXX:SF, SF in key, or value containing "Sunfly"
    if (key.includes("TXXX:SF") || key === "SF" || val.includes("SUNFLY") || val === "SF") {
      return "Sunfly";
    }

    // Karaoke.com: TXXX:KCOM, KCOM in key, or value containing "Karaoke.com" / "KaraokeCom"
    if (key.includes("TXXX:KCOM") || key === "KCOM" || key === "KARAOKECOM" || val.includes("KARAOKE.COM") || val.includes("KCOM")) {
      return "Karaoke.com";
    }
  }

  return null;
}

/**
 * Detects provider from MP4 container metadata fields (title, artist, comment).
 */
export function detectProviderFromMp4(metadata: FFprobeResult): TrackSource | null {
  if (!metadata) return null;
  const text = [
    metadata.title || "",
    metadata.artist || "",
    metadata.comment || "",
    ...(metadata.tags ? Object.values(metadata.tags) : []),
  ].join(" ").toUpperCase();

  if (text.includes("SUNFLY")) return "Sunfly";
  if (text.includes("PARTY TYME") || text.includes("PARTYTYME") || text.includes("SYBERSOUND")) return "Party Tyme";
  if (text.includes("KARAOKE VERSION") || text.includes("KARAOKE-VERSION")) return "Karaoke Version";
  if (text.includes("KARAOKE.COM") || text.includes("KARAOKEDOTCOM")) return "Karaoke.com";

  return null;
}

/**
 * Normalizes audio loudness using FFmpeg's EBU R128 loudnorm filter (-16 LUFS).
 */
export async function normalizeAudio(inputPath: string, outputPath?: string): Promise<string> {
  const destPath = outputPath || getProcessedPath(inputPath, "_norm");

  return new Promise((resolve, reject) => {
    const args = [
      "-y",
      "-i", inputPath,
      "-af", "loudnorm=I=-16:TP=-1.5:LRA=11",
      "-c:a", "libmp3lame",
      "-b:a", "320k",
      destPath,
    ];

    execFile("ffmpeg", args, (err) => {
      if (err) {
        reject(err);
      } else {
        resolve(destPath);
      }
    });
  });
}

/**
 * Trims leading and trailing silence from an audio file using FFmpeg's silenceremove filter.
 */
export async function trimSilence(inputPath: string, outputPath?: string): Promise<string> {
  const destPath = outputPath || getProcessedPath(inputPath, "_trimmed");

  return new Promise((resolve, reject) => {
    // Trims leading silence then reverses, trims leading silence, and reverses back
    const filter = "silenceremove=start_periods=1:start_duration=0.1:start_threshold=-50dB:detection=peak,areverse,silenceremove=start_periods=1:start_duration=0.1:start_threshold=-50dB:detection=peak,areverse";

    const args = [
      "-y",
      "-i", inputPath,
      "-af", filter,
      "-c:a", "libmp3lame",
      "-b:a", "320k",
      destPath,
    ];

    execFile("ffmpeg", args, (err) => {
      if (err) {
        reject(err);
      } else {
        resolve(destPath);
      }
    });
  });
}

/**
 * Generates a PNG visual waveform image for the audio file.
 */
export async function generateWaveformPreview(inputPath: string, outputPngPath?: string): Promise<string> {
  const destPng = outputPngPath || path.join(path.dirname(inputPath), `${path.basename(inputPath, path.extname(inputPath))}_waveform.png`);

  return new Promise((resolve, reject) => {
    const args = [
      "-y",
      "-i", inputPath,
      "-filter_complex", "aformat=channel_layouts=mono,showwavespic=s=800x160:colors=#00C9FF|#8E2DE2",
      "-frames:v", "1",
      destPng,
    ];

    execFile("ffmpeg", args, (err) => {
      if (err) {
        reject(err);
      } else {
        resolve(destPng);
      }
    });
  });
}

function getProcessedPath(origPath: string, suffix: string): string {
  const dir = path.dirname(origPath);
  const ext = path.extname(origPath);
  const name = path.basename(origPath, ext);
  return path.join(dir, `${name}${suffix}${ext}`);
}

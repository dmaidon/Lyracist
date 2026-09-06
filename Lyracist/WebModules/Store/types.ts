// Edited on Sep 6, 2026 @ 11:16:00 -> Extend Track schema with Smart Import fields: Genre, Difficulty, Key, BPM, VocalPresence, Quality

export type TrackSource = "Local" | "Karaoke Version" | "Party Tyme" | "Karaoke.com" | "Sunfly";
export type ProviderSource = TrackSource;

export type KaraokeFormat = "MP3G" | "ZIPCDG" | "MP4" | "Audio";
export type DifficultyLevel = "Easy" | "Medium" | "Hard";
export type VocalPresence = "guide vocals" | "background vocals" | "no vocals";
export type QualityLevel = "Low" | "Medium" | "High";

export interface Track {
  id: string;
  title: string;
  artist: string;
  source: TrackSource;
  filePaths: string[];
  karaokeType?: KaraokeFormat;
  isKaraoke?: boolean;
  addedAt: Date;
  normalized?: boolean;
  silenceTrimmed?: boolean;
  waveformPath?: string;
  lyricsPath?: string;
  hasDualAudio?: boolean;
  duration?: number;
  bitrate?: number;
  codec?: string;
  sampleRate?: number;
  channels?: number;
  videoStreamInfo?: string;
  genre?: string;
  difficulty?: DifficultyLevel;
  key?: string;
  bpm?: number;
  vocalPresence?: VocalPresence;
  quality?: QualityLevel;
}

export interface StoreSettings {
  purchasedTracksFolder: string;
  targetKaraokeFolder: string;
  targetMusicFolder: string;
  autoImportEnabled: boolean;
  moveFilesToTarget: boolean;
  normalizeAudioOnImport: boolean;
  trimSilenceOnImport: boolean;
  generateWaveformOnImport: boolean;
}

export interface ImportLogEvent {
  id: string;
  timestamp: Date;
  trackTitle: string;
  artist: string;
  source: string;
  status: "Success" | "Processing" | "Warning" | "Error";
  message: string;
  details?: string;
}

export interface ParsedTrackMetadata {
  title: string;
  artist: string;
  source: TrackSource;
  karaokeType: KaraokeFormat;
  isKaraoke: boolean;
  lyricsPath?: string;
  duration?: number;
  bitrate?: number;
  codec?: string;
  sampleRate?: number;
  channels?: number;
  videoStreamInfo?: string;
  hasDualAudio?: boolean;
  genre?: string;
  difficulty?: DifficultyLevel;
  key?: string;
  bpm?: number;
  vocalPresence?: VocalPresence;
  quality?: QualityLevel;
}

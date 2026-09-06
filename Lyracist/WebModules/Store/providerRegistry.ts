// Created on Sep 6, 2026 @ 12:50:00 -> Implement modular Store Plugin API and ProviderRegistry in WebModules
import * as path from "path";
import * as fs from "fs";
import { ProviderSource, TrackSource } from "./types";
import { FFprobeResult } from "./ffmpegUtils";

/**
 * Interface defining a commercial karaoke store provider plugin.
 */
export interface IStoreProvider {
  readonly name: string;
  readonly source: ProviderSource;
  buildSearchUrl(query: string): string;
  detectFromFilename(filename: string): boolean;
  detectFromId3(metadata: FFprobeResult): boolean;
  detectFromZip(zipPath: string): boolean;
  detectFromCdgHeader(header: Buffer): boolean;
  detectFromMp4(metadata: FFprobeResult): boolean;
}

/**
 * Abstract base class with default detection returning false and shared inspection utilities.
 */
export abstract class BaseStoreProvider implements IStoreProvider {
  public abstract readonly name: string;
  public abstract readonly source: ProviderSource;

  public abstract buildSearchUrl(query: string): string;

  public detectFromFilename(filename: string): boolean {
    return false;
  }

  public detectFromId3(metadata: FFprobeResult): boolean {
    return false;
  }

  public detectFromZip(zipPath: string): boolean {
    return false;
  }

  public detectFromCdgHeader(header: Buffer): boolean {
    return false;
  }

  public detectFromMp4(metadata: FFprobeResult): boolean {
    return false;
  }

  /**
   * Safely scans ZIP archive headers for internal entry names.
   */
  protected readZipEntries(zipPath: string): string[] {
    try {
      if (!fs.existsSync(zipPath)) return [];
      const buffer = fs.readFileSync(zipPath);
      const entryNames: string[] = [];
      let offset = 0;

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
}

/**
 * Store plugin for Karaoke-Version.com.
 */
export class KaraokeVersionProvider extends BaseStoreProvider {
  public readonly name = "Karaoke Version";
  public readonly source: ProviderSource = "Karaoke Version";

  public buildSearchUrl(query: string): string {
    const trimmed = query.trim();
    if (!trimmed) return "https://www.karaoke-version.com/";
    return `https://www.karaoke-version.com/search.html?q=${encodeURIComponent(trimmed)}`;
  }

  public detectFromFilename(filename: string): boolean {
    const lower = filename.toLowerCase();
    return (
      lower.includes("karaoke version") ||
      lower.includes("karaoke-version") ||
      /\bkv[-\s]?\d+/i.test(filename)
    );
  }

  public detectFromId3(metadata: FFprobeResult): boolean {
    if (!metadata?.tags) return false;
    for (const [key, val] of Object.entries(metadata.tags)) {
      const upperKey = key.toUpperCase();
      const upperVal = (val || "").toUpperCase();
      if (
        upperKey.includes("TXXX:KV") ||
        upperKey === "KV" ||
        upperVal.includes("KARAOKE VERSION") ||
        upperVal === "KV"
      ) {
        return true;
      }
    }
    return false;
  }

  public detectFromZip(zipPath: string): boolean {
    const entries = this.readZipEntries(zipPath).map((e) => e.replace(/\\/g, "/").toLowerCase());
    if (entries.length === 0) return false;

    const hasCustomBackingTrack = entries.some(
      (e) => e.includes("custom_backing_track/") || e.startsWith("custom_backing_track/")
    );
    const hasKvName = entries.some(
      (e) => e.includes("karaoke version") || /\bkv[-\s]?\d+/i.test(e)
    );
    const hasGenericTrackPair =
      entries.some((e) => path.basename(e) === "track.mp3") &&
      entries.some((e) => path.basename(e) === "track.cdg");

    return hasCustomBackingTrack || hasKvName || hasGenericTrackPair;
  }

  public detectFromCdgHeader(header: Buffer): boolean {
    return header.length >= 2 && header[0] === 0x01 && header[1] === 0x0f;
  }

  public detectFromMp4(metadata: FFprobeResult): boolean {
    if (!metadata) return false;
    const combined = `${metadata.title || ""} ${metadata.artist || ""} ${metadata.comment || ""} ${JSON.stringify(metadata.tags || {})}`.toUpperCase();
    return combined.includes("KARAOKE VERSION") || combined.includes("KARAOKE-VERSION");
  }
}

/**
 * Store plugin for Party Tyme Karaoke.
 */
export class PartyTymeProvider extends BaseStoreProvider {
  public readonly name = "Party Tyme";
  public readonly source: ProviderSource = "Party Tyme";

  public buildSearchUrl(query: string): string {
    const trimmed = query.trim();
    if (!trimmed) return "https://www.partytyme.net/";
    return `https://www.partytyme.net/search?q=${encodeURIComponent(trimmed)}`;
  }

  public detectFromFilename(filename: string): boolean {
    const lower = filename.toLowerCase();
    return (
      lower.includes("party tyme") ||
      lower.includes("partytyme") ||
      lower.includes("sybersound") ||
      /\bpt[-\s]?\d+/i.test(filename)
    );
  }

  public detectFromId3(metadata: FFprobeResult): boolean {
    if (!metadata?.tags) return false;
    for (const [key, val] of Object.entries(metadata.tags)) {
      const upperKey = key.toUpperCase();
      const upperVal = (val || "").toUpperCase();
      if (
        upperKey.includes("TXXX:PT") ||
        upperKey === "PT" ||
        upperVal.includes("PARTY TYME") ||
        upperVal === "PT" ||
        upperVal.includes("SYBERSOUND")
      ) {
        return true;
      }
    }
    return false;
  }

  public detectFromZip(zipPath: string): boolean {
    const entries = this.readZipEntries(zipPath).map((e) => e.replace(/\\/g, "/").toLowerCase());
    if (entries.length === 0) return false;

    const hasKaraokeFolder = entries.some(
      (e) => e.includes("/karaoke/") || e.startsWith("karaoke/")
    );
    const hasPtName = entries.some(
      (e) =>
        e.includes("_pt.") ||
        e.includes("- pt.") ||
        e.includes("party tyme") ||
        /\bpt[-\s]?\d+/i.test(e)
    );

    return hasKaraokeFolder || hasPtName;
  }

  public detectFromCdgHeader(header: Buffer): boolean {
    return header.length >= 2 && header[0] === 0x02 && header[1] === 0x0a;
  }

  public detectFromMp4(metadata: FFprobeResult): boolean {
    if (!metadata) return false;
    const combined = `${metadata.title || ""} ${metadata.artist || ""} ${metadata.comment || ""} ${JSON.stringify(metadata.tags || {})}`.toUpperCase();
    return combined.includes("PARTY TYME") || combined.includes("PARTYTYME") || combined.includes("SYBERSOUND");
  }
}

/**
 * Store plugin for Sunfly Karaoke.
 */
export class SunflyProvider extends BaseStoreProvider {
  public readonly name = "Sunfly";
  public readonly source: ProviderSource = "Sunfly";

  public buildSearchUrl(query: string): string {
    const trimmed = query.trim();
    if (!trimmed) return "https://www.sunflykaraoke.com/";
    return `https://www.sunflykaraoke.com/catalogsearch/result/?q=${encodeURIComponent(trimmed)}`;
  }

  public detectFromFilename(filename: string): boolean {
    const lower = filename.toLowerCase();
    return lower.includes("sunfly") || /\bsf[-\s]?\d+/i.test(filename);
  }

  public detectFromId3(metadata: FFprobeResult): boolean {
    if (!metadata?.tags) return false;
    for (const [key, val] of Object.entries(metadata.tags)) {
      const upperKey = key.toUpperCase();
      const upperVal = (val || "").toUpperCase();
      if (
        upperKey.includes("TXXX:SF") ||
        upperKey === "SF" ||
        upperVal.includes("SUNFLY") ||
        upperVal === "SF"
      ) {
        return true;
      }
    }
    return false;
  }

  public detectFromZip(zipPath: string): boolean {
    const entries = this.readZipEntries(zipPath);
    return entries.some((e) => {
      const base = path.basename(e);
      return base.toUpperCase().startsWith("SF") || e.toLowerCase().includes("sunfly");
    });
  }

  public detectFromCdgHeader(header: Buffer): boolean {
    return header.length >= 2 && header[0] === 0x03 && header[1] === 0x0c;
  }

  public detectFromMp4(metadata: FFprobeResult): boolean {
    if (!metadata) return false;
    const combined = `${metadata.title || ""} ${metadata.artist || ""} ${metadata.comment || ""} ${JSON.stringify(metadata.tags || {})}`.toUpperCase();
    return combined.includes("SUNFLY");
  }
}

/**
 * Store plugin for Karaoke.com.
 */
export class KaraokeComProvider extends BaseStoreProvider {
  public readonly name = "Karaoke.com";
  public readonly source: ProviderSource = "Karaoke.com";

  public buildSearchUrl(query: string): string {
    const trimmed = query.trim();
    if (!trimmed) return "https://karaoke.com/";
    return `https://karaoke.com/search?type=product&q=${encodeURIComponent(trimmed)}`;
  }

  public detectFromFilename(filename: string): boolean {
    const lower = filename.toLowerCase();
    return lower.includes("karaoke.com") || lower.includes("karaokedotcom");
  }

  public detectFromId3(metadata: FFprobeResult): boolean {
    if (!metadata?.tags) return false;
    for (const [key, val] of Object.entries(metadata.tags)) {
      const upperKey = key.toUpperCase();
      const upperVal = (val || "").toUpperCase();
      if (
        upperKey.includes("TXXX:KCOM") ||
        upperKey === "KCOM" ||
        upperKey === "KARAOKECOM" ||
        upperVal.includes("KARAOKE.COM") ||
        upperVal.includes("KCOM")
      ) {
        return true;
      }
    }
    return false;
  }

  public detectFromZip(zipPath: string): boolean {
    const entries = this.readZipEntries(zipPath).map((e) => e.toLowerCase());
    return entries.some(
      (e) => e.includes("kcom") || e.includes("karaokecom") || e.includes("karaoke.com")
    );
  }

  public detectFromCdgHeader(header: Buffer): boolean {
    return false;
  }

  public detectFromMp4(metadata: FFprobeResult): boolean {
    if (!metadata) return false;
    const combined = `${metadata.title || ""} ${metadata.artist || ""} ${metadata.comment || ""} ${JSON.stringify(metadata.tags || {})}`.toUpperCase();
    return combined.includes("KARAOKE.COM") || combined.includes("KARAOKEDOTCOM");
  }
}

/**
 * Central registry managing store provider plugins.
 */
export class ProviderRegistry {
  private providers: IStoreProvider[] = [];

  constructor() {
    this.register(new KaraokeVersionProvider());
    this.register(new PartyTymeProvider());
    this.register(new SunflyProvider());
    this.register(new KaraokeComProvider());
  }

  public register(provider: IStoreProvider): void {
    this.providers = this.providers.filter(
      (p) => p.source !== provider.source && p.name !== provider.name
    );
    this.providers.push(provider);
  }

  public getAllProviders(): IStoreProvider[] {
    return [...this.providers];
  }

  public getProviderBySource(source: ProviderSource): IStoreProvider | undefined {
    return this.providers.find((p) => p.source === source);
  }

  public getProviderByName(name: string): IStoreProvider | undefined {
    const trimmed = name.trim().toLowerCase();
    return this.providers.find(
      (p) => p.name.toLowerCase() === trimmed || p.source.toLowerCase() === trimmed
    );
  }

  public detectProviderFromFilename(filename: string): IStoreProvider | undefined {
    return this.providers.find((p) => p.detectFromFilename(filename));
  }

  public detectProviderFromZip(zipPath: string): IStoreProvider | undefined {
    return this.providers.find((p) => p.detectFromZip(zipPath));
  }

  public detectProviderFromCdg(headerOrPath: Buffer | string): IStoreProvider | undefined {
    let header: Buffer | null = null;
    if (Buffer.isBuffer(headerOrPath)) {
      header = headerOrPath;
    } else if (typeof headerOrPath === "string" && fs.existsSync(headerOrPath)) {
      try {
        const fd = fs.openSync(headerOrPath, "r");
        const buf = Buffer.alloc(24);
        const bytesRead = fs.readSync(fd, buf, 0, 24, 0);
        fs.closeSync(fd);
        if (bytesRead >= 2) {
          header = buf;
        }
      } catch {
        return undefined;
      }
    }

    if (!header || header.length < 2) return undefined;
    return this.providers.find((p) => p.detectFromCdgHeader(header!));
  }

  public detectProviderFromId3(metadata: FFprobeResult): IStoreProvider | undefined {
    return this.providers.find((p) => p.detectFromId3(metadata));
  }

  public detectProviderFromMp4(metadata: FFprobeResult): IStoreProvider | undefined {
    return this.providers.find((p) => p.detectFromMp4(metadata));
  }

  public detectProviderFromMetadata(metadata: FFprobeResult): IStoreProvider | undefined {
    return this.detectProviderFromId3(metadata) || this.detectProviderFromMp4(metadata);
  }
}

export const providerRegistry = new ProviderRegistry();

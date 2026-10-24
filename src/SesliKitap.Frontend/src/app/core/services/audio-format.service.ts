import { Injectable, signal } from '@angular/core';
import { AudioFormat } from '../models/enums';

export type AudioFormatPreference = 'auto' | 'opus' | 'mp3';

const FORMAT_PREF_KEY = 'sk.audioFormat';
const SKIP_BACK_KEY = 'sk.skipBackSec';
const SKIP_FORWARD_KEY = 'sk.skipForwardSec';
const DEFAULT_RATE_KEY = 'sk.defaultRate';
const READER_FONT_SIZE_KEY = 'sk.readerFontSize';

@Injectable({ providedIn: 'root' })
export class AudioFormatService {
  private readonly opusSupported = this.detectOpusSupport();

  readonly preference = signal<AudioFormatPreference>(this.readPreference());
  readonly skipBackSec = signal<number>(this.readNumber(SKIP_BACK_KEY, 15));
  readonly skipForwardSec = signal<number>(this.readNumber(SKIP_FORWARD_KEY, 30));
  readonly defaultRate = signal<number>(this.readNumber(DEFAULT_RATE_KEY, 1.0));
  readonly readerFontSize = signal<number>(this.readNumber(READER_FONT_SIZE_KEY, 18));

  preferred(): AudioFormat {
    const pref = this.preference();
    if (pref === 'opus' || pref === 'mp3') {
      return pref;
    }
    return this.opusSupported ? 'opus' : 'mp3';
  }

  setPreference(pref: AudioFormatPreference): void {
    this.preference.set(pref);
    try {
      localStorage.setItem(FORMAT_PREF_KEY, pref);
    } catch {
      // ignore
    }
  }

  setSkipIntervals(backSec: number, forwardSec: number): void {
    this.skipBackSec.set(backSec);
    this.skipForwardSec.set(forwardSec);
    try {
      localStorage.setItem(SKIP_BACK_KEY, String(backSec));
      localStorage.setItem(SKIP_FORWARD_KEY, String(forwardSec));
    } catch {
      // ignore
    }
  }

  setDefaultRate(rate: number): void {
    const clamped = Math.min(3.0, Math.max(0.5, Math.round(rate * 100) / 100));
    this.defaultRate.set(clamped);
    try {
      localStorage.setItem(DEFAULT_RATE_KEY, String(clamped));
    } catch {
      // ignore
    }
  }

  setReaderFontSize(px: number): void {
    const clamped = Math.min(28, Math.max(14, px));
    this.readerFontSize.set(clamped);
    try {
      localStorage.setItem(READER_FONT_SIZE_KEY, String(clamped));
    } catch {
      // ignore
    }
  }

  private detectOpusSupport(): boolean {
    try {
      if (typeof Audio === 'undefined') {
        return true;
      }
      const probe = new Audio();
      const res = probe.canPlayType('audio/ogg; codecs="opus"');
      return res === 'probably' || res === 'maybe';
    } catch {
      return true;
    }
  }

  private readPreference(): AudioFormatPreference {
    try {
      const saved = localStorage.getItem(FORMAT_PREF_KEY);
      if (saved === 'opus' || saved === 'mp3' || saved === 'auto') {
        return saved;
      }
    } catch {
      // ignore
    }
    return 'auto';
  }

  private readNumber(key: string, fallback: number): number {
    try {
      const val = Number(localStorage.getItem(key));
      return Number.isFinite(val) && val > 0 ? val : fallback;
    } catch {
      return fallback;
    }
  }
}

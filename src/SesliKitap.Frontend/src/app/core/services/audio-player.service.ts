import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { BookDetail } from '../models/book.model';
import { ChapterSummary } from '../models/chapter.model';
import { ChapterTimestamps, SentenceTs } from '../models/timestamps.model';
import { AudioFormatService } from './audio-format.service';
import { BookService } from './book.service';
import { ChapterService } from './chapter.service';
import { MediaSessionService } from './media-session.service';
import { MediaTokenService } from './media-token.service';
import { OfflineStorageService } from './offline-storage.service';
import { ProgressService } from './progress.service';
import { SignalrService } from './signalr.service';
import { SleepTimerService } from './sleep-timer.service';
import { ToastService } from './toast.service';

/**
 * Verilen milisaniye konumuna karşılık gelen cümleyi ikili arama (binary search) ile bulur.
 */
export function findSentence(
  timestamps: ChapterTimestamps | null,
  positionMs: number
): SentenceTs | null {
  if (!timestamps?.sentences?.length || positionMs < 0) {
    return null;
  }

  const list = timestamps.sentences;
  let low = 0;
  let high = list.length - 1;
  let candidate: SentenceTs | null = null;

  while (low <= high) {
    const mid = (low + high) >>> 1;
    const item = list[mid];
    if (item.s <= positionMs) {
      candidate = item;
      low = mid + 1;
    } else {
      high = mid - 1;
    }
  }

  return candidate;
}

@Injectable({ providedIn: 'root' })
export class AudioPlayerService {
  private readonly bookService = inject(BookService);
  private readonly chapterService = inject(ChapterService);
  private readonly mediaToken = inject(MediaTokenService);
  private readonly formatService = inject(AudioFormatService);
  private readonly offlineStorage = inject(OfflineStorageService);
  private readonly progressService = inject(ProgressService);
  private readonly mediaSession = inject(MediaSessionService);
  private readonly sleepTimer = inject(SleepTimerService);
  private readonly signalr = inject(SignalrService);
  private readonly toast = inject(ToastService);

  private audio: HTMLAudioElement;
  private reportIntervalId: ReturnType<typeof setInterval> | null = null;
  private prefetchedChapterId: string | null = null;
  private retriedAuthForCurrentSrc = false;

  readonly book = signal<BookDetail | null>(null);
  readonly chapter = signal<ChapterSummary | null>(null);
  readonly isPlaying = signal(false);
  readonly isBuffering = signal(false);
  readonly position = signal(0);
  readonly duration = signal(0);
  readonly bufferedEnd = signal(0);
  readonly rate = signal(1.0);
  readonly timestamps = signal<ChapterTimestamps | null>(null);

  readonly currentSentence = computed(() => findSentence(this.timestamps(), this.position() * 1000));

  readonly playableChapters = computed(
    () =>
      this.book()?.bolumler.filter(
        (c) => c.dahilMi && (c.sesDurumu === 'Hazir' || c.sesDurumu === 'GuncelDegil')
      ) ?? []
  );

  constructor() {
    this.audio = typeof Audio !== 'undefined' ? new Audio() : ({} as HTMLAudioElement);
    this.configureAudioElement(this.audio);
    this.mediaSession.bindHandlers(this);

    this.sleepTimer.registerCallbacks({
      onVolumeChange: (v) => {
        if (this.audio) {
          this.audio.volume = Math.min(1, Math.max(0, v));
        }
      },
      onPause: () => {
        this.pause();
      }
    });

    this.setupLifecycleReporting();
    this.setupRemoteProgressSync();
  }

  /**
   * Birim testlerinde sahte HTMLAudioElement enjekte etmek için kullanılır.
   */
  setAudioElementForTest(fakeAudio: HTMLAudioElement): void {
    this.audio = fakeAudio;
    this.configureAudioElement(this.audio);
  }

  async loadBook(
    bookId: string,
    opts?: { chapterId?: string; positionSec?: number; autoplay?: boolean }
  ): Promise<void> {
    let bookDetail: BookDetail | null = null;
    try {
      bookDetail = await firstValueFrom(this.bookService.getById(bookId));
    } catch {
      const offlineMeta = await this.offlineStorage.getBookMeta(bookId);
      bookDetail = offlineMeta?.book ?? null;
    }

    if (!bookDetail) {
      this.toast.error('Kitap bilgileri yüklenemedi.');
      return;
    }

    this.book.set(bookDetail);
    void this.signalr.joinBook(bookDetail.id);

    const savedRate = this.readBookRate(bookDetail.id);
    this.setRate(savedRate, false);

    let targetChapterId = opts?.chapterId;
    let targetPos = opts?.positionSec ?? 0;

    if (!targetChapterId) {
      try {
        const prog = await firstValueFrom(this.progressService.get(bookDetail.id));
        if (prog) {
          targetChapterId = prog.chapterId;
          targetPos = opts?.positionSec ?? prog.pozisyonSn;
          if (prog.oynatmaHizi >= 0.5 && prog.oynatmaHizi <= 3.0) {
            this.setRate(prog.oynatmaHizi, false);
          }
        }
      } catch {
        // ignore
      }
    }

    const playable = this.playableChapters();
    const chosen =
      playable.find((c) => c.id === targetChapterId) ??
      playable[0] ??
      null;

    if (!chosen) {
      this.toast.warning('Bu kitapta henüz dinlenmeye hazır bölüm bulunmuyor.');
      return;
    }

    await this.playChapter(chosen.id, targetPos, opts?.autoplay ?? true);
  }

  async playChapter(chapterId: string, positionSec = 0, autoplay = true): Promise<void> {
    const currentBook = this.book();
    if (!currentBook) return;

    const target = currentBook.bolumler.find((c) => c.id === chapterId);
    if (!target) return;

    this.chapter.set(target);
    this.position.set(positionSec);
    this.duration.set(target.sureSn ?? target.tahminiSureSn ?? 0);
    this.prefetchedChapterId = null;
    this.retriedAuthForCurrentSrc = false;

    void this.loadTimestamps(currentBook.id, target.id);

    const format = this.formatService.preferred();
    const offlineUrl = await this.offlineStorage.getAudioUrl(target.id, format);
    const audioUrl = offlineUrl ?? (await this.mediaToken.audioUrl(target.id, format));

    this.audio.src = audioUrl;
    this.applyPitchPreservation();
    this.audio.playbackRate = this.rate();
    this.audio.currentTime = Math.max(0, positionSec);

    void this.updateMediaSessionMetadata(currentBook, target);

    if (autoplay) {
      await this.play();
    } else {
      this.pause();
    }
  }

  async play(): Promise<void> {
    try {
      this.applyPitchPreservation();
      this.audio.playbackRate = this.rate();
      await this.audio.play();
      this.isPlaying.set(true);
    } catch {
      this.isPlaying.set(false);
    }
  }

  pause(): void {
    try {
      this.audio.pause();
    } catch {
      // ignore
    }
    this.isPlaying.set(false);
    this.reportCurrentProgress();
  }

  toggle(): void {
    if (this.isPlaying()) {
      this.pause();
    } else {
      void this.play();
    }
  }

  seek(sec: number): void {
    const dur = this.duration() || this.audio.duration || 0;
    const clamped = dur > 0 ? Math.min(Math.max(0, sec), dur) : Math.max(0, sec);
    this.audio.currentTime = clamped;
    this.position.set(clamped);
    this.mediaSession.setPositionState(dur, clamped, this.rate());
  }

  skip(deltaSec: number): void {
    this.seek(this.position() + deltaSec);
  }

  setRate(newRate: number, persist = true): void {
    const clamped = Math.min(3.0, Math.max(0.5, Math.round(newRate * 100) / 100));
    this.rate.set(clamped);
    if (this.audio) {
      this.applyPitchPreservation();
      this.audio.playbackRate = clamped;
    }

    const currentBook = this.book();
    if (persist && currentBook) {
      try {
        localStorage.setItem(`sk.rate.${currentBook.id}`, String(clamped));
      } catch {
        // ignore
      }
      this.reportCurrentProgress();
    }

    this.mediaSession.setPositionState(this.duration(), this.position(), clamped);
  }

  async nextChapter(): Promise<void> {
    const playable = this.playableChapters();
    const current = this.chapter();
    if (!current || playable.length === 0) return;

    const idx = playable.findIndex((c) => c.id === current.id);
    if (idx >= 0 && idx + 1 < playable.length) {
      await this.playChapter(playable[idx + 1].id, 0, true);
    } else {
      // Son bölüm tamamlandı -> %100 olarak kaydet
      this.pause();
      const b = this.book();
      if (b) {
        void this.progressService.reportPosition(
          b.id,
          current.id,
          this.duration(),
          this.rate(),
          true,
          true
        );
      }
    }
  }

  async prevChapter(): Promise<void> {
    if (this.position() > 5) {
      this.seek(0);
      return;
    }

    const playable = this.playableChapters();
    const current = this.chapter();
    if (!current || playable.length === 0) return;

    const idx = playable.findIndex((c) => c.id === current.id);
    if (idx > 0) {
      await this.playChapter(playable[idx - 1].id, 0, true);
    } else {
      this.seek(0);
    }
  }

  seekToSentence(sentenceIndex: number): void {
    const ts = this.timestamps();
    const target = ts?.sentences.find((s) => s.i === sentenceIndex);
    if (!target) return;

    this.seek(target.s / 1000);
    if (!this.isPlaying()) {
      void this.play();
    }
  }

  private configureAudioElement(el: HTMLAudioElement): void {
    if (!el || typeof el.addEventListener !== 'function') {
      return;
    }

    el.preload = 'auto';
    this.applyPitchPreservation(el);

    el.addEventListener('timeupdate', () => {
      const pos = el.currentTime || 0;
      this.position.set(pos);
      this.updateBufferedEnd(el);
      this.maybePrefetchNextChapter(pos);
    });

    el.addEventListener('loadedmetadata', () => {
      if (Number.isFinite(el.duration) && el.duration > 0) {
        this.duration.set(el.duration);
        this.mediaSession.setPositionState(el.duration, el.currentTime || 0, this.rate());
      }
    });

    el.addEventListener('waiting', () => this.isBuffering.set(true));
    el.addEventListener('playing', () => {
      this.isBuffering.set(false);
      this.isPlaying.set(true);
    });
    el.addEventListener('pause', () => this.isPlaying.set(false));

    el.addEventListener('ended', () => {
      if (this.sleepTimer.consumeEndOfChapterStop()) {
        this.pause();
        return;
      }
      void this.nextChapter();
    });

    el.addEventListener('error', () => {
      void this.handleAudioError();
    });
  }

  private applyPitchPreservation(el: HTMLAudioElement = this.audio): void {
    if (!el) return;
    const anyAudio = el as HTMLAudioElement & {
      preservesPitch?: boolean;
      mozPreservesPitch?: boolean;
      webkitPreservesPitch?: boolean;
    };
    anyAudio.preservesPitch = true;
    anyAudio.mozPreservesPitch = true;
    anyAudio.webkitPreservesPitch = true;
  }

  private updateBufferedEnd(el: HTMLAudioElement): void {
    try {
      if (el.buffered && el.buffered.length > 0) {
        this.bufferedEnd.set(el.buffered.end(el.buffered.length - 1));
      }
    } catch {
      // ignore
    }
  }

  private maybePrefetchNextChapter(currentPosSec: number): void {
    const dur = this.duration();
    if (dur <= 0 || dur - currentPosSec > 60) {
      return;
    }

    const playable = this.playableChapters();
    const current = this.chapter();
    if (!current) return;

    const idx = playable.findIndex((c) => c.id === current.id);
    if (idx < 0 || idx + 1 >= playable.length) return;

    const nextCh = playable[idx + 1];
    if (this.prefetchedChapterId === nextCh.id) return;
    this.prefetchedChapterId = nextCh.id;

    void (async () => {
      try {
        const format = this.formatService.preferred();
        const hasOffline = await this.offlineStorage.hasAudio(nextCh.id, format);
        if (hasOffline) return;

        const url = await this.mediaToken.audioUrl(nextCh.id, format);
        await fetch(url, {
          headers: { Range: 'bytes=0-262143' }
        });
      } catch {
        // ignore prefetch errors
      }
    })();
  }

  private async handleAudioError(): Promise<void> {
    const ch = this.chapter();
    if (!ch || this.retriedAuthForCurrentSrc) {
      this.toast.error('Ses akışı oynatılırken bir hata oluştu.');
      return;
    }

    this.retriedAuthForCurrentSrc = true;
    const savedTime = this.position();
    const wasPlaying = this.isPlaying();

    try {
      this.mediaToken.invalidate();
      const format = this.formatService.preferred();
      const refreshedUrl = await this.mediaToken.audioUrl(ch.id, format);
      this.audio.src = refreshedUrl;
      this.audio.currentTime = savedTime;
      if (wasPlaying) {
        await this.play();
      }
    } catch {
      this.toast.error('Medya erişim belirteci yenilenemedi.');
    }
  }

  private async loadTimestamps(bookId: string, chapterId: string): Promise<void> {
    this.timestamps.set(null);
    const offlineTs = await this.offlineStorage.getOfflineTimestamps(bookId, chapterId);
    if (offlineTs) {
      this.timestamps.set(offlineTs);
      return;
    }

    try {
      const ts = await firstValueFrom(this.chapterService.getTimestamps(chapterId));
      this.timestamps.set(ts);
    } catch {
      this.timestamps.set(null);
    }
  }

  private async updateMediaSessionMetadata(book: BookDetail, chapter: ChapterSummary): Promise<void> {
    const artworkUrl = book.kapakVar ? await this.mediaToken.coverUrl(book.id) : '';
    this.mediaSession.update({
      title: `${chapter.siraNo}. Bölüm: ${chapter.baslik}`,
      artist: book.yazar || 'SesliKitap',
      album: book.baslik,
      artworkUrl
    });
  }

  private setupLifecycleReporting(): void {
    if (typeof window === 'undefined') return;

    this.reportIntervalId = setInterval(() => {
      if (this.isPlaying()) {
        this.reportCurrentProgress();
        this.mediaSession.setPositionState(this.duration(), this.position(), this.rate());
      }
    }, 10000);

    document.addEventListener('visibilitychange', () => {
      if (document.visibilityState === 'hidden') {
        this.reportCurrentProgress(true);
      }
    });

    window.addEventListener('pagehide', () => {
      this.reportCurrentProgress(true);
    });
  }

  private setupRemoteProgressSync(): void {
    this.signalr.progressUpdated$.subscribe((msg) => {
      const currentBook = this.book();
      if (!currentBook || currentBook.id !== msg.bookId) return;

      const currentCh = this.chapter();
      const isDifferentChapter = currentCh?.id !== msg.chapterId;
      const posDiff = Math.abs(this.position() - msg.pozisyonSn);

      if (isDifferentChapter || posDiff > 30) {
        const mins = Math.floor(msg.pozisyonSn / 60);
        const secs = Math.floor(msg.pozisyonSn % 60)
          .toString()
          .padStart(2, '0');
        const deviceLabel = msg.cihazAdi || 'Başka cihaz';

        this.toast.info(
          `${deviceLabel} üzerinde ${msg.bolumSiraNo}. Bölüm ${mins}:${secs} konumunda bıraktınız.`,
          undefined,
          {
            label: 'Oraya git',
            callback: () => {
              void this.playChapter(msg.chapterId, msg.pozisyonSn, this.isPlaying());
            }
          }
        );
      }
    });
  }

  private reportCurrentProgress(force = false): void {
    const b = this.book();
    const ch = this.chapter();
    if (!b || !ch) return;

    void this.progressService.reportPosition(
      b.id,
      ch.id,
      this.position(),
      this.rate(),
      false,
      force
    );
  }

  private readBookRate(bookId: string): number {
    try {
      const raw = localStorage.getItem(`sk.rate.${bookId}`);
      if (raw) {
        const val = Number(raw);
        if (Number.isFinite(val) && val >= 0.5 && val <= 3.0) {
          return val;
        }
      }
    } catch {
      // ignore
    }
    return this.formatService.defaultRate();
  }
}

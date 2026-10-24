import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { BookDetail } from '../models/book.model';
import { ChapterTimestamps } from '../models/timestamps.model';
import { AudioFormatService } from './audio-format.service';
import { BookService } from './book.service';
import { ChapterService } from './chapter.service';
import { MediaTokenService } from './media-token.service';
import { OfflineStorageService } from './offline-storage.service';
import { ToastService } from './toast.service';

export interface ActiveDownloadState {
  bookId: string;
  bookTitle: string;
  currentChapterIndex: number;
  totalChapters: number;
  percent: number;
  downloadedBytes: number;
}

@Injectable({ providedIn: 'root' })
export class DownloadManagerService {
  private readonly bookService = inject(BookService);
  private readonly chapterService = inject(ChapterService);
  private readonly mediaToken = inject(MediaTokenService);
  private readonly formatService = inject(AudioFormatService);
  private readonly offlineStorage = inject(OfflineStorageService);
  private readonly toast = inject(ToastService);

  readonly activeDownload = signal<ActiveDownloadState | null>(null);
  readonly queue = signal<string[]>([]);
  private abortController: AbortController | null = null;

  async enqueueBook(bookId: string): Promise<void> {
    if (this.activeDownload()?.bookId === bookId || this.queue().includes(bookId)) {
      this.toast.info('Bu kitap zaten indirme kuyruğunda.');
      return;
    }

    this.queue.update((q) => [...q, bookId]);
    if (!this.activeDownload()) {
      await this.processNext();
    }
  }

  cancelCurrent(): void {
    if (this.abortController) {
      this.abortController.abort();
      this.abortController = null;
    }
    this.activeDownload.set(null);
    this.toast.info('İndirme iptal edildi.');
    void this.processNext();
  }

  private async processNext(): Promise<void> {
    const currentQueue = this.queue();
    if (currentQueue.length === 0) {
      this.activeDownload.set(null);
      return;
    }

    const nextBookId = currentQueue[0];
    this.queue.set(currentQueue.slice(1));

    try {
      await this.downloadBookInternal(nextBookId);
    } catch (err) {
      if ((err as Error)?.name !== 'AbortError') {
        this.toast.error('Çevrimdışı indirme sırasında hata oluştu.');
      }
    } finally {
      this.activeDownload.set(null);
      this.abortController = null;
      if (this.queue().length > 0) {
        await this.processNext();
      }
    }
  }

  private async downloadBookInternal(bookId: string): Promise<void> {
    const book: BookDetail = await firstValueFrom(this.bookService.getById(bookId));
    const readyChapters = book.bolumler.filter(
      (c) => c.dahilMi && (c.sesDurumu === 'Hazir' || c.sesDurumu === 'GuncelDegil')
    );

    if (readyChapters.length === 0) {
      this.toast.warning('Bu kitapta indirilebilecek hazır sesli bölüm yok.');
      return;
    }

    this.abortController = new AbortController();
    const signal = this.abortController.signal;
    const format = this.formatService.preferred();

    this.activeDownload.set({
      bookId: book.id,
      bookTitle: book.baslik,
      currentChapterIndex: 0,
      totalChapters: readyChapters.length,
      percent: 0,
      downloadedBytes: 0
    });

    const timestampsMap: Record<string, ChapterTimestamps> = {};
    const downloadedChapterIds: string[] = [];
    let totalBytes = 0;

    for (let i = 0; i < readyChapters.length; i++) {
      if (signal.aborted) {
        throw new DOMException('Aborted', 'AbortError');
      }

      const ch = readyChapters[i];
      const audioUrl = await this.mediaToken.audioUrl(ch.id, format);

      const response = await fetch(audioUrl, { signal });
      if (!response.ok || !response.body) {
        throw new Error(`HTTP ${response.status}`);
      }

      const contentLength = Number(response.headers.get('Content-Length') ?? 0);
      const reader = response.body.getReader();
      const chunks: Uint8Array[] = [];
      let receivedForChapter = 0;

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        if (value) {
          chunks.push(value);
          receivedForChapter += value.byteLength;
          const chapterProgress = contentLength > 0 ? receivedForChapter / contentLength : 0.5;
          const overallPercent = Math.min(
            99,
            Math.round(((i + chapterProgress) / readyChapters.length) * 100)
          );
          this.activeDownload.update((prev) =>
            prev
              ? {
                  ...prev,
                  currentChapterIndex: i + 1,
                  percent: overallPercent,
                  downloadedBytes: totalBytes + receivedForChapter
                }
              : null
          );
        }
      }

      const mime = format === 'opus' ? 'audio/ogg' : 'audio/mpeg';
      const blob = new Blob(chunks as BlobPart[], { type: mime });
      totalBytes += blob.size;

      await this.offlineStorage.saveAudio({
        bookId: book.id,
        chapterId: ch.id,
        format,
        metinSurumu: ch.metinSurumu,
        size: blob.size,
        savedAt: new Date().toISOString(),
        blob
      });

      try {
        const ts = await firstValueFrom(this.chapterService.getTimestamps(ch.id));
        timestampsMap[ch.id] = ts;
      } catch {
        // ignore missing timestamps
      }

      downloadedChapterIds.push(ch.id);
    }

    let coverBlob: Blob | null = null;
    if (book.kapakVar) {
      try {
        const coverUrl = await this.mediaToken.coverUrl(book.id);
        const coverResp = await fetch(coverUrl, { signal });
        if (coverResp.ok) {
          coverBlob = await coverResp.blob();
        }
      } catch {
        // ignore
      }
    }

    await this.offlineStorage.saveBookMeta({
      bookId: book.id,
      book,
      timestamps: timestampsMap,
      coverBlob,
      totalBytes,
      downloadedChapterIds,
      savedAt: new Date().toISOString()
    });

    this.toast.success(`"${book.baslik}" çevrimdışı dinleme için indirildi.`);
  }
}

import { Injectable } from '@angular/core';
import { DBSchema, IDBPDatabase, openDB } from 'idb';
import { BookDetail } from '../models/book.model';
import { AudioFormat } from '../models/enums';
import { ProgressUpdateRequest } from '../models/progress.model';
import { ChapterTimestamps } from '../models/timestamps.model';

export interface StoredAudioRecord {
  key: string;
  bookId: string;
  chapterId: string;
  format: AudioFormat;
  metinSurumu: number;
  size: number;
  savedAt: string;
  blob: Blob;
}

export interface StoredBookMetaRecord {
  bookId: string;
  book: BookDetail;
  timestamps: Record<string, ChapterTimestamps>;
  coverBlob?: Blob | null;
  totalBytes: number;
  downloadedChapterIds: string[];
  savedAt: string;
}

export interface PendingProgressRecord {
  bookId: string;
  request: ProgressUpdateRequest;
}

interface SesliKitapDbSchema extends DBSchema {
  audio: {
    key: string;
    value: StoredAudioRecord;
    indexes: { 'by-book': string };
  };
  'book-meta': {
    key: string;
    value: StoredBookMetaRecord;
  };
  'pending-progress': {
    key: string;
    value: PendingProgressRecord;
  };
}

@Injectable({ providedIn: 'root' })
export class OfflineStorageService {
  private dbPromise: Promise<IDBPDatabase<SesliKitapDbSchema>> | null = null;
  private readonly activeObjectUrls = new Map<string, string>();
  private persistRequested = false;

  private getDb(): Promise<IDBPDatabase<SesliKitapDbSchema>> {
    if (!this.dbPromise) {
      this.dbPromise = openDB<SesliKitapDbSchema>('seslikitap', 1, {
        upgrade(db) {
          if (!db.objectStoreNames.contains('audio')) {
            const audioStore = db.createObjectStore('audio', { keyPath: 'key' });
            audioStore.createIndex('by-book', 'bookId');
          }
          if (!db.objectStoreNames.contains('book-meta')) {
            db.createObjectStore('book-meta', { keyPath: 'bookId' });
          }
          if (!db.objectStoreNames.contains('pending-progress')) {
            db.createObjectStore('pending-progress', { keyPath: 'bookId' });
          }
        }
      });
    }
    return this.dbPromise;
  }

  async ensurePersistentStorage(): Promise<void> {
    if (this.persistRequested) return;
    this.persistRequested = true;
    try {
      if (navigator.storage && navigator.storage.persist) {
        await navigator.storage.persist();
      }
    } catch {
      // ignore
    }
  }

  async saveAudio(record: Omit<StoredAudioRecord, 'key'>): Promise<void> {
    await this.ensurePersistentStorage();
    const db = await this.getDb();
    const key = `${record.chapterId}:${record.format}`;
    await db.put('audio', { ...record, key });
  }

  async getAudioUrl(chapterId: string, format: AudioFormat): Promise<string | null> {
    try {
      const db = await this.getDb();
      const key = `${chapterId}:${format}`;
      const record = await db.get('audio', key);
      if (!record?.blob) {
        return null;
      }

      const prevUrl = this.activeObjectUrls.get(key);
      if (prevUrl) {
        URL.revokeObjectURL(prevUrl);
      }

      const objUrl = URL.createObjectURL(record.blob);
      this.activeObjectUrls.set(key, objUrl);
      return objUrl;
    } catch {
      return null;
    }
  }

  async hasAudio(chapterId: string, format?: AudioFormat): Promise<boolean> {
    try {
      const db = await this.getDb();
      if (format) {
        const rec = await db.get('audio', `${chapterId}:${format}`);
        return Boolean(rec);
      }
      const opus = await db.get('audio', `${chapterId}:opus`);
      if (opus) return true;
      const mp3 = await db.get('audio', `${chapterId}:mp3`);
      return Boolean(mp3);
    } catch {
      return false;
    }
  }

  async saveBookMeta(record: StoredBookMetaRecord): Promise<void> {
    await this.ensurePersistentStorage();
    const db = await this.getDb();
    await db.put('book-meta', record);
  }

  async getBookMeta(bookId: string): Promise<StoredBookMetaRecord | null> {
    try {
      const db = await this.getDb();
      return (await db.get('book-meta', bookId)) ?? null;
    } catch {
      return null;
    }
  }

  async getOfflineTimestamps(bookId: string, chapterId: string): Promise<ChapterTimestamps | null> {
    const meta = await this.getBookMeta(bookId);
    return meta?.timestamps?.[chapterId] ?? null;
  }

  async listDownloadedBooks(): Promise<StoredBookMetaRecord[]> {
    try {
      const db = await this.getDb();
      return await db.getAll('book-meta');
    } catch {
      return [];
    }
  }

  async deleteBook(bookId: string): Promise<void> {
    const db = await this.getDb();
    const audioRecords = await db.getAllFromIndex('audio', 'by-book', bookId);
    const tx = db.transaction(['audio', 'book-meta'], 'readwrite');
    for (const rec of audioRecords) {
      const url = this.activeObjectUrls.get(rec.key);
      if (url) {
        URL.revokeObjectURL(url);
        this.activeObjectUrls.delete(rec.key);
      }
      await tx.objectStore('audio').delete(rec.key);
    }
    await tx.objectStore('book-meta').delete(bookId);
    await tx.done;
  }

  async deleteAll(): Promise<void> {
    for (const url of this.activeObjectUrls.values()) {
      URL.revokeObjectURL(url);
    }
    this.activeObjectUrls.clear();
    const db = await this.getDb();
    const tx = db.transaction(['audio', 'book-meta'], 'readwrite');
    await tx.objectStore('audio').clear();
    await tx.objectStore('book-meta').clear();
    await tx.done;
  }

  async savePendingProgress(bookId: string, request: ProgressUpdateRequest): Promise<void> {
    const db = await this.getDb();
    await db.put('pending-progress', { bookId, request });
  }

  async getAllPendingProgress(): Promise<PendingProgressRecord[]> {
    try {
      const db = await this.getDb();
      return await db.getAll('pending-progress');
    } catch {
      return [];
    }
  }

  async deletePendingProgress(bookId: string): Promise<void> {
    try {
      const db = await this.getDb();
      await db.delete('pending-progress', bookId);
    } catch {
      // ignore
    }
  }

  async usage(): Promise<{ used: number; quota: number }> {
    try {
      if (navigator.storage && navigator.storage.estimate) {
        const est = await navigator.storage.estimate();
        return {
          used: est.usage ?? 0,
          quota: est.quota ?? 0
        };
      }
    } catch {
      // ignore
    }
    return { used: 0, quota: 0 };
  }
}

import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { T } from '../../core/i18n/tr';
import { DownloadManagerService } from '../../core/services/download-manager.service';
import {
  OfflineStorageService,
  StoredBookMetaRecord
} from '../../core/services/offline-storage.service';
import { ToastService } from '../../core/services/toast.service';
import { ConfirmService } from '../../shared/components/confirm-dialog.component';
import { CoverImageComponent } from '../../shared/components/cover-image.component';
import { EmptyStateComponent } from '../../shared/components/empty-state.component';
import { ProgressBarComponent } from '../../shared/components/progress-bar.component';
import { FileSizePipe } from '../../shared/pipes/file-size.pipe';

@Component({
  selector: 'app-downloads-page',
  standalone: true,
  imports: [
    RouterLink,
    DatePipe,
    FileSizePipe,
    CoverImageComponent,
    EmptyStateComponent,
    ProgressBarComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="max-w-6xl mx-auto px-4 md:px-6 py-6 space-y-6">
      <!-- Başlık ve Depolama Özeti -->
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 class="text-2xl font-bold text-slate-900 dark:text-slate-100">
            {{ t.downloads.title }}
          </h1>
          <p class="text-xs text-slate-500 dark:text-slate-400 mt-1">
            {{ t.downloads.storageUsed }}:
            <strong class="text-slate-800 dark:text-slate-200">{{ usedBytes() | fileSize }}</strong>
            @if (quotaBytes() > 0) {
              <span> / {{ quotaBytes() | fileSize }}</span>
            }
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            type="button"
            (click)="loadDownloads()"
            class="px-3.5 py-2 min-h-[40px] rounded-xl border border-slate-200 dark:border-slate-800 text-xs font-semibold text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition"
          >
            Yenile
          </button>
          @if (books().length > 0) {
            <button
              type="button"
              (click)="deleteAll()"
              class="px-3.5 py-2 min-h-[40px] rounded-xl border border-rose-500/30 text-rose-500 hover:bg-rose-500/10 text-xs font-semibold transition"
            >
              {{ t.downloads.deleteAll }}
            </button>
          }
        </div>
      </div>

      <!-- Devam Eden İndirme -->
      @if (downloadManager.activeDownload(); as active) {
        <div
          class="p-4 rounded-2xl bg-brand-500/10 border border-brand-500/20 text-slate-900 dark:text-slate-100 space-y-3"
        >
          <div class="flex items-center justify-between gap-3">
            <div>
              <div class="text-xs font-bold text-brand-600 dark:text-brand-400">
                Şu anda indiriliyor
              </div>
              <div class="text-sm font-semibold truncate">{{ active.bookTitle }}</div>
            </div>
            <button
              type="button"
              (click)="downloadManager.cancelCurrent()"
              class="px-3 py-1.5 rounded-lg bg-rose-500/15 text-rose-500 hover:bg-rose-500/25 text-xs font-semibold transition"
            >
              İptal Et
            </button>
          </div>

          <app-progress-bar [value]="active.percent" />

          <div class="flex items-center justify-between text-xs text-slate-500 dark:text-slate-400">
            <span>Bölüm {{ active.currentChapterIndex + 1 }} / {{ active.totalChapters }}</span>
            <span>{{ active.downloadedBytes | fileSize }} (%{{ active.percent }})</span>
          </div>
        </div>
      }

      <!-- İndirilen Kitaplar Listesi -->
      @if (loading()) {
        <div class="text-center py-12 text-sm text-slate-400">
          {{ t.common.loading }}
        </div>
      } @else if (books().length === 0) {
        <app-empty-state
          [title]="t.downloads.empty"
          actionLabel="Kütüphaneye Git"
          (actionClick)="goToLibrary()"
        />
      } @else {
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          @for (item of books(); track item.bookId) {
            <div
              class="flex flex-col justify-between p-4 rounded-2xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-800 shadow-xs hover:border-slate-300 dark:hover:border-slate-700 transition"
            >
              <div class="flex gap-4">
                <div class="w-18 h-26 shrink-0 rounded-lg overflow-hidden">
                  <app-cover-image
                    [bookId]="item.bookId"
                    [title]="item.book.baslik"
                    [hasCover]="item.book.kapakVar"
                  />
                </div>
                <div class="flex-1 min-w-0">
                  <h3
                    class="font-bold text-sm text-slate-900 dark:text-slate-100 truncate"
                    [title]="item.book.baslik"
                  >
                    {{ item.book.baslik }}
                  </h3>
                  <p class="text-xs text-slate-500 dark:text-slate-400 truncate mt-0.5">
                    {{ item.book.yazar || 'Bilinmeyen Yazar' }}
                  </p>

                  <div class="mt-3 flex flex-wrap gap-2 text-[11px] text-slate-500 dark:text-slate-400">
                    <span class="px-2 py-0.5 rounded-md bg-slate-100 dark:bg-slate-700">
                      {{ item.downloadedChapterIds.length }} bölüm
                    </span>
                    <span class="px-2 py-0.5 rounded-md bg-slate-100 dark:bg-slate-700">
                      {{ item.totalBytes | fileSize }}
                    </span>
                  </div>

                  <p class="text-[10px] text-slate-400 dark:text-slate-500 mt-2">
                    {{ item.savedAt | date: 'mediumDate' }}
                  </p>
                </div>
              </div>

              <div class="mt-4 pt-3 border-t border-slate-100 dark:border-slate-800 flex items-center justify-between gap-2">
                <div class="flex items-center gap-2">
                  <a
                    [routerLink]="['/dinle', item.bookId]"
                    class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl bg-brand-500 hover:bg-brand-400 text-slate-950 font-bold text-xs transition"
                  >
                    <span>▶</span>
                    <span>{{ t.bookDetail.listen }}</span>
                  </a>
                  <a
                    [routerLink]="['/kitap', item.bookId]"
                    class="px-3 py-1.5 rounded-xl bg-slate-100 dark:bg-slate-700 hover:bg-slate-200 dark:hover:bg-slate-600 text-xs font-semibold text-slate-700 dark:text-slate-200 transition"
                  >
                    Detay
                  </a>
                </div>

                <button
                  type="button"
                  (click)="deleteBook(item)"
                  title="{{ t.common.delete }}"
                  class="p-2 rounded-xl text-rose-500 hover:bg-rose-500/10 transition text-xs font-semibold"
                >
                  <svg
                    class="w-4 h-4"
                    fill="none"
                    stroke="currentColor"
                    viewBox="0 0 24 24"
                  >
                    <path
                      stroke-linecap="round"
                      stroke-linejoin="round"
                      stroke-width="2"
                      d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"
                    />
                  </svg>
                </button>
              </div>
            </div>
          }
        </div>
      }
    </div>
  `
})
export class DownloadsPageComponent implements OnInit {
  protected readonly t = T;
  protected readonly downloadManager = inject(DownloadManagerService);
  private readonly offlineStorage = inject(OfflineStorageService);
  private readonly confirm = inject(ConfirmService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  readonly books = signal<StoredBookMetaRecord[]>([]);
  readonly loading = signal<boolean>(true);
  readonly usedBytes = signal<number>(0);
  readonly quotaBytes = signal<number>(0);

  async ngOnInit(): Promise<void> {
    await this.loadDownloads();
  }

  async loadDownloads(): Promise<void> {
    this.loading.set(true);
    try {
      const [list, usage] = await Promise.all([
        this.offlineStorage.listDownloadedBooks(),
        this.offlineStorage.usage()
      ]);
      this.books.set(list);
      this.usedBytes.set(usage.used);
      this.quotaBytes.set(usage.quota);
    } catch {
      this.toast.error('İndirilen kitaplar listelenirken hata oluştu.');
    } finally {
      this.loading.set(false);
    }
  }

  async deleteBook(item: StoredBookMetaRecord): Promise<void> {
    const ok = await this.confirm.ask({
      title: 'Kitabı Çevrimdışından Sil',
      message: `"${item.book.baslik}" çevrimdışı depolamadan silinecek. Emin misiniz?`,
      danger: true
    });
    if (!ok) return;

    try {
      await this.offlineStorage.deleteBook(item.bookId);
      this.toast.success('Kitap çevrimdışı depolamadan silindi.');
      await this.loadDownloads();
    } catch {
      this.toast.error('Kitap silinirken hata oluştu.');
    }
  }

  async deleteAll(): Promise<void> {
    const ok = await this.confirm.ask({
      title: 'Tümünü Sil',
      message: 'Cihazdaki tüm indirilmiş sesli kitaplar silinecek. Emin misiniz?',
      danger: true
    });
    if (!ok) return;

    try {
      await this.offlineStorage.deleteAll();
      this.toast.success('Tüm çevrimdışı kitaplar silindi.');
      await this.loadDownloads();
    } catch {
      this.toast.error('Depolama temizlenirken hata oluştu.');
    }
  }

  goToLibrary(): void {
    void this.router.navigate(['/']);
  }
}

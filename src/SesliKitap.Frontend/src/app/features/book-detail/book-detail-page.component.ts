import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  input,
  signal
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { T } from '../../core/i18n/tr';
import { BookDetail } from '../../core/models/book.model';
import { ChapterSummary } from '../../core/models/chapter.model';
import { ProgressDto } from '../../core/models/progress.model';
import { BookService } from '../../core/services/book.service';
import { DownloadManagerService } from '../../core/services/download-manager.service';
import { MediaTokenService } from '../../core/services/media-token.service';
import { ProgressService } from '../../core/services/progress.service';
import { SignalrService } from '../../core/services/signalr.service';
import { ToastService } from '../../core/services/toast.service';
import { ConfirmService } from '../../shared/components/confirm-dialog.component';
import { CoverImageComponent } from '../../shared/components/cover-image.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge.component';
import { ClockPipe } from '../../shared/pipes/clock.pipe';
import { DurationPipe } from '../../shared/pipes/duration.pipe';
import { FileSizePipe } from '../../shared/pipes/file-size.pipe';
import { ChapterEditorComponent } from './chapter-editor.component';
import { ChapterListComponent } from './chapter-list.component';
import { ProcessingStatusComponent } from './processing-status.component';
import { SynthesisPanelComponent } from './synthesis-panel.component';

@Component({
  selector: 'app-book-detail-page',
  standalone: true,
  imports: [
    RouterLink,
    CoverImageComponent,
    StatusBadgeComponent,
    ClockPipe,
    DurationPipe,
    FileSizePipe,
    ProcessingStatusComponent,
    ChapterListComponent,
    ChapterEditorComponent,
    SynthesisPanelComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="max-w-7xl mx-auto px-4 md:px-6 py-6 space-y-6">
      @if (loading()) {
        <div class="h-48 rounded-2xl bg-slate-200 dark:bg-surface-800 animate-pulse"></div>
      } @else {
        @if (book(); as b) {
        <!-- Üst Başlık Kartı -->
        <div
          class="rounded-2xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-800 p-5 sm:p-6 flex flex-col sm:flex-row gap-6"
        >
          <div class="w-32 sm:w-40 aspect-[3/4] shrink-0 mx-auto sm:mx-0">
            <app-cover-image [bookId]="b.id" [title]="b.baslik" [hasCover]="b.kapakVar" />
          </div>

          <div class="flex-1 flex flex-col justify-between space-y-4">
            <div>
              <div class="flex flex-wrap items-center gap-2.5">
                <app-status-badge [status]="b.durum" kind="book" />
                <span class="text-xs text-slate-400">
                  {{ b.format }} · {{ b.dosyaBoyutu | fileSize }}
                </span>
              </div>

              <h1 class="mt-2 text-2xl font-bold text-slate-900 dark:text-slate-100">
                {{ b.baslik }}
              </h1>
              <p class="text-sm text-slate-500 dark:text-slate-400">
                {{ b.yazar || 'Bilinmeyen Yazar' }}
              </p>

              <div class="mt-3 flex flex-wrap gap-4 text-xs text-slate-500 dark:text-slate-400">
                <span>{{ b.hazirBolumSayisi }} / {{ b.bolumSayisi }} bölüm hazır</span>
                <span>{{ b.toplamKelimeSayisi }} kelime</span>
                @if (b.toplamSureSn > 0) {
                  <span>Toplam süre: {{ b.toplamSureSn | duration }}</span>
                }
              </div>
            </div>

            <div class="flex flex-wrap items-center gap-2.5">
              @if (b.hazirBolumSayisi > 0) {
                <a
                  [routerLink]="['/dinle', b.id]"
                  class="min-h-[44px] px-5 py-2.5 rounded-xl bg-brand-500 hover:bg-brand-600 text-slate-950 font-semibold text-sm flex items-center gap-2 transition"
                >
                  @if (progress(); as p) {
                    ▶ Devam et ({{ p.bolumSiraNo }}. Bölüm {{ p.pozisyonSn | clock }})
                  } @else {
                    ▶ {{ t.bookDetail.listen }}
                  }
                </a>
                <button
                  type="button"
                  (click)="downloadOffline()"
                  class="min-h-[44px] px-4 py-2.5 rounded-xl bg-slate-100 dark:bg-slate-700 hover:bg-slate-200 dark:hover:bg-slate-600 text-sm font-semibold transition"
                >
                  ⬇ {{ t.bookDetail.downloadOffline }}
                </button>
                <div class="relative">
                  <button
                    type="button"
                    (click)="exportMenuOpen.set(!exportMenuOpen())"
                    class="min-h-[44px] px-4 py-2.5 rounded-xl bg-slate-100 dark:bg-slate-700 hover:bg-slate-200 dark:hover:bg-slate-600 text-sm font-semibold transition"
                  >
                    📦 {{ t.bookDetail.export }} ▾
                  </button>
                  @if (exportMenuOpen()) {
                    <div
                      class="absolute left-0 mt-2 w-48 rounded-xl bg-white dark:bg-surface-900 border border-slate-200 dark:border-slate-700 shadow-xl py-1 z-20"
                    >
                      <button
                        type="button"
                        (click)="exportZip('opus')"
                        class="w-full text-left px-4 py-2.5 text-xs font-medium hover:bg-slate-100 dark:hover:bg-slate-800 transition"
                      >
                        ZIP İndir (Opus)
                      </button>
                      <button
                        type="button"
                        (click)="exportZip('mp3')"
                        class="w-full text-left px-4 py-2.5 text-xs font-medium hover:bg-slate-100 dark:hover:bg-slate-800 transition"
                      >
                        ZIP İndir (MP3)
                      </button>
                      <button
                        type="button"
                        (click)="exportM4b()"
                        class="w-full text-left px-4 py-2.5 text-xs font-medium hover:bg-slate-100 dark:hover:bg-slate-800 transition border-t border-slate-200 dark:border-slate-800"
                      >
                        M4B İndir (Apple Books)
                      </button>
                    </div>
                  }
                </div>
              }
              <button
                type="button"
                (click)="activeTab.set('synthesis')"
                class="min-h-[44px] px-4 py-2.5 rounded-xl bg-slate-100 dark:bg-slate-700 hover:bg-slate-200 dark:hover:bg-slate-600 text-sm font-semibold transition"
              >
                🎙 {{ t.bookDetail.synthesize }}
              </button>
              <button
                type="button"
                (click)="reprocessBook()"
                class="min-h-[44px] px-4 py-2.5 rounded-xl border border-slate-200 dark:border-slate-700 hover:bg-slate-100 dark:hover:bg-slate-700 text-xs font-medium transition"
              >
                ↻ {{ t.bookDetail.reprocess }}
              </button>
              <button
                type="button"
                (click)="deleteBook()"
                class="min-h-[44px] px-4 py-2.5 rounded-xl border border-rose-500/30 text-rose-400 hover:bg-rose-500/10 text-xs font-medium transition"
              >
                🗑 {{ t.bookDetail.deleteBook }}
              </button>
            </div>
          </div>
        </div>

        <!-- İşleniyor Durumu -->
        @if (b.durum === 'Isleniyor' || b.durum === 'Indiriliyor' || b.durum === 'Hata') {
          <app-processing-status
            [progress]="signalr.bookProcessingProgress()[b.id] ?? null"
            [errorMessage]="b.hataMesaji"
            (retry)="reprocessBook()"
          />
        }

        <!-- OCR Gerekli Uyarısı -->
        @if (b.durum === 'OcrGerekli') {
          <div
            class="p-4 rounded-2xl bg-amber-500/15 border border-amber-500/30 text-amber-300 text-sm flex flex-wrap items-center justify-between gap-4"
          >
            <span>{{ t.bookDetail.ocrWarning }}</span>
            <button
              type="button"
              (click)="startOcr()"
              class="px-4 py-2 min-h-[40px] rounded-xl bg-amber-500 hover:bg-amber-400 text-slate-950 text-xs font-bold whitespace-nowrap transition"
            >
              🔍 {{ t.bookDetail.runOcr }}
            </button>
          </div>
        }

        <!-- Sekmeler -->
        <div class="border-b border-slate-200 dark:border-slate-800 flex items-center gap-2">
          <button
            type="button"
            (click)="activeTab.set('chapters')"
            class="px-4 py-3 text-sm font-semibold border-b-2 transition"
            [class]="
              activeTab() === 'chapters'
                ? 'border-brand-500 text-brand-400'
                : 'border-transparent text-slate-500 hover:text-slate-300'
            "
          >
            {{ t.bookDetail.tabChapters }} ({{ b.bolumler.length }})
          </button>
          <button
            type="button"
            (click)="activeTab.set('synthesis')"
            class="px-4 py-3 text-sm font-semibold border-b-2 transition"
            [class]="
              activeTab() === 'synthesis'
                ? 'border-brand-500 text-brand-400'
                : 'border-transparent text-slate-500 hover:text-slate-300'
            "
          >
            {{ t.bookDetail.tabSynthesis }}
          </button>
          <button
            type="button"
            (click)="activeTab.set('info')"
            class="px-4 py-3 text-sm font-semibold border-b-2 transition"
            [class]="
              activeTab() === 'info'
                ? 'border-brand-500 text-brand-400'
                : 'border-transparent text-slate-500 hover:text-slate-300'
            "
          >
            {{ t.bookDetail.tabInfo }}
          </button>
        </div>

        <!-- Sekme İçerikleri -->
        @switch (activeTab()) {
          @case ('chapters') {
            <app-chapter-list
              [bookId]="b.id"
              [chapters]="b.bolumler"
              [isLocked]="b.durum === 'Seslendiriliyor'"
              (chaptersUpdated)="onChaptersUpdated($event)"
              (editChapter)="editingChapterId.set($event)"
              (synthesizeSelected)="onSynthesizeSelected($event)"
            />
          }
          @case ('synthesis') {
            <app-synthesis-panel
              [bookId]="b.id"
              [defaultVoiceId]="b.varsayilanSesProfiliId"
              [preselectedChapterIds]="selectedForSynthesis()"
              (jobStarted)="loadBook()"
            />
          }
          @case ('info') {
            <div
              class="rounded-2xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-800 p-6 space-y-4 text-sm"
            >
              <h3 class="font-bold text-slate-900 dark:text-slate-100">
                Metin Çıkarma ve Temizleme İstatistikleri
              </h3>
              @if (b.cikarimIstatistikleri; as stats) {
                <div class="grid grid-cols-2 sm:grid-cols-4 gap-4">
                  <div class="p-3.5 rounded-xl bg-slate-50 dark:bg-surface-900">
                    <span class="text-xs text-slate-400 block">Bölüm Tespit Yöntemi</span>
                    <span class="font-semibold">{{ stats.tespitYontemi }}</span>
                  </div>
                  <div class="p-3.5 rounded-xl bg-slate-50 dark:bg-surface-900">
                    <span class="text-xs text-slate-400 block">Silinen Üst/Alt Bilgi</span>
                    <span class="font-semibold">{{ stats.silinenUstAltBilgiSayisi }}</span>
                  </div>
                  <div class="p-3.5 rounded-xl bg-slate-50 dark:bg-surface-900">
                    <span class="text-xs text-slate-400 block">Silinen Dipnot</span>
                    <span class="font-semibold">{{ stats.silinenDipnotSayisi }}</span>
                  </div>
                  <div class="p-3.5 rounded-xl bg-slate-50 dark:bg-surface-900">
                    <span class="text-xs text-slate-400 block">Birleştirilen Tireli Kelime</span>
                    <span class="font-semibold">{{ stats.birlestirilenTireliKelimeSayisi }}</span>
                  </div>
                </div>
              } @else {
                <p class="text-slate-400">Bu kitap için çıkarım istatistiği bulunmuyor.</p>
              }
            </div>
          }
        }

        <app-chapter-editor
          [chapterId]="editingChapterId()"
          (closed)="editingChapterId.set(null)"
          (saved)="loadBook()"
          (chaptersChanged)="onChaptersUpdated($event)"
        />
        }
      }
    </div>
  `
})
export class BookDetailPageComponent implements OnInit {
  private readonly bookService = inject(BookService);
  private readonly progressService = inject(ProgressService);
  private readonly downloadManager = inject(DownloadManagerService);
  private readonly mediaToken = inject(MediaTokenService);
  readonly signalr = inject(SignalrService);
  private readonly confirm = inject(ConfirmService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly id = input.required<string>();

  readonly t = T;
  readonly book = signal<BookDetail | null>(null);
  readonly progress = signal<ProgressDto | null>(null);
  readonly loading = signal(true);
  readonly exportMenuOpen = signal(false);
  readonly activeTab = signal<'chapters' | 'synthesis' | 'info'>('chapters');
  readonly editingChapterId = signal<string | null>(null);
  readonly selectedForSynthesis = signal<string[]>([]);

  readonly bookIdComputed = computed(() => this.id());

  ngOnInit(): void {
    this.loadBook();

    const currentId = this.id();
    void this.signalr.joinBook(currentId);
    this.destroyRef.onDestroy(() => {
      void this.signalr.leaveBook(currentId);
    });

    this.signalr.bookStatusChanged$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((msg) => {
        if (msg.bookId === this.id()) {
          this.loadBook();
        }
      });

    this.signalr.chapterReady$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((msg) => {
        if (msg.bookId === this.id()) {
          this.toast.success(`${msg.siraNo}. Bölüm hazır: ${msg.baslik}`, undefined, {
            label: 'Dinle',
            callback: () => {
              void this.router.navigate(['/dinle', this.id()], {
                queryParams: { bolum: msg.chapterId }
              });
            }
          });
          this.loadBook();
        }
      });

    this.signalr.synthesisFinished$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((msg) => {
        if (msg.bookId === this.id()) {
          this.loadBook();
        }
      });

    this.signalr.exportReady$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((msg) => {
        if (msg.bookId === this.id()) {
          if (msg.basariliMi) {
            this.toast.success('M4B sesli kitap dosyası hazır!', undefined, {
              label: 'İndir',
              callback: () => {
                void this.downloadReadyM4b();
              }
            });
          } else {
            this.toast.error(msg.hataMesaji || 'M4B dışa aktarma başarısız oldu.');
          }
        }
      });
  }

  loadBook(): void {
    this.bookService.getById(this.id()).subscribe({
      next: (det) => {
        this.book.set(det);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      }
    });
    this.progressService.get(this.id()).subscribe((prog) => {
      this.progress.set(prog);
    });
  }

  downloadOffline(): void {
    const b = this.book();
    if (!b) return;
    void this.downloadManager.enqueueBook(b.id).then(() => {
      this.toast.info('Çevrimdışı indirme başlatıldı.');
    });
  }

  async exportZip(format: 'opus' | 'mp3'): Promise<void> {
    this.exportMenuOpen.set(false);
    const b = this.book();
    if (!b) return;
    try {
      const url = await this.mediaToken.exportZipUrl(b.id, format);
      window.open(url, '_blank');
    } catch {
      this.toast.error('Dışa aktarma bağlantısı oluşturulamadı.');
    }
  }

  async exportM4b(): Promise<void> {
    this.exportMenuOpen.set(false);
    const b = this.book();
    if (!b) return;
    try {
      const url = await this.mediaToken.exportM4bUrl(b.id);
      const res = await fetch(url, { method: 'GET' });
      if (res.status === 202) {
        this.toast.info('M4B dosyası hazırlanıyor. Tamamlandığında bildirim alacaksınız.');
        return;
      }
      if (res.ok) {
        await res.body?.cancel();
        window.open(url, '_blank');
        return;
      }
      this.toast.error('M4B dışa aktarma başlatılamadı.');
    } catch {
      this.toast.error('M4B dışa aktarma bağlantısı oluşturulamadı.');
    }
  }

  private async downloadReadyM4b(): Promise<void> {
    const b = this.book();
    if (!b) return;
    const url = await this.mediaToken.exportM4bUrl(b.id);
    window.open(url, '_blank');
  }

  startOcr(): void {
    const b = this.book();
    if (!b) return;
    this.bookService.startOcr(b.id).subscribe({
      next: () => {
        this.toast.info('OCR metin çıkarma işlemi başlatıldı.');
        this.loadBook();
      }
    });
  }

  onChaptersUpdated(chapters: ChapterSummary[]): void {
    this.book.update((prev) => (prev ? { ...prev, bolumler: chapters } : prev));
  }

  onSynthesizeSelected(ids: string[]): void {
    this.selectedForSynthesis.set(ids);
    this.activeTab.set('synthesis');
  }

  async reprocessBook(): Promise<void> {
    const ok = await this.confirm.ask({
      title: 'Kitabı Yeniden İşle',
      message: 'Kaynak dosya yeniden ayrıştırılacak. Devam etmek istiyor musunuz?'
    });
    if (!ok) return;

    this.bookService.reprocess(this.id()).subscribe(() => {
      this.toast.info('Kitap yeniden işleme kuyruğuna alındı.');
      this.loadBook();
    });
  }

  async deleteBook(): Promise<void> {
    const ok = await this.confirm.ask({
      title: 'Kitabı Sil',
      message: 'Bu kitabı kütüphanenizden silmek istediğinize emin misiniz?',
      danger: true
    });
    if (!ok) return;

    this.bookService.delete(this.id()).subscribe(() => {
      this.toast.success('Kitap silindi.');
      void this.router.navigate(['/']);
    });
  }
}

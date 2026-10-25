import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  ViewChild,
  computed,
  effect,
  inject,
  input,
  output,
  signal
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { T } from '../../core/i18n/tr';
import { ChapterDetail, ChapterSummary } from '../../core/models/chapter.model';
import { PronunciationPreviewResponse } from '../../core/models/faz8.model';
import { ChapterService } from '../../core/services/chapter.service';
import { PronunciationService } from '../../core/services/pronunciation.service';
import { ToastService } from '../../core/services/toast.service';
import { DurationPipe } from '../../shared/pipes/duration.pipe';

@Component({
  selector: 'app-chapter-editor',
  standalone: true,
  imports: [FormsModule, DurationPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (chapterId()) {
      <div
        class="fixed inset-0 z-50 flex justify-end bg-black/60 backdrop-blur-xs"
        role="dialog"
        aria-modal="true"
      >
        <div
          class="w-full max-w-3xl h-full bg-white dark:bg-surface-900 border-l border-slate-200 dark:border-slate-800 flex flex-col shadow-2xl"
        >
          <div
            class="px-6 py-4 border-b border-slate-200 dark:border-slate-800 flex items-center justify-between gap-3"
          >
            <div>
              <h2 class="text-base font-bold text-slate-900 dark:text-slate-100">
                {{ t.bookDetail.editChapter }}
              </h2>
              <p class="text-xs text-slate-500 dark:text-slate-400">
                {{ wordCount() }} kelime · Tahmini süre: {{ estimatedSec() | duration }}
              </p>
            </div>
            <button
              type="button"
              (click)="close()"
              class="min-w-[44px] min-h-[44px] rounded-xl flex items-center justify-center text-slate-400 hover:text-slate-200"
            >
              ✕
            </button>
          </div>

          @if (loading()) {
            <div class="flex-1 flex items-center justify-center text-sm text-slate-400">
              {{ t.common.loading }}
            </div>
          } @else {
            <div class="flex-1 overflow-y-auto p-6 space-y-4 flex flex-col">
              @if (conflictDetected()) {
                <div
                  class="p-3.5 rounded-xl bg-amber-500/15 border border-amber-500/40 text-amber-300 text-xs flex items-center justify-between gap-3"
                >
                  <span>{{ t.bookDetail.conflictWarning }}</span>
                  <button
                    type="button"
                    (click)="reloadChapter()"
                    class="px-3 py-1.5 rounded-lg bg-amber-500 text-slate-950 font-semibold whitespace-nowrap"
                  >
                    Yeniden Yükle
                  </button>
                </div>
              }

              <div>
                <label class="block text-xs font-medium text-slate-500 dark:text-slate-400 mb-1">
                  Bölüm Başlığı
                </label>
                <input
                  type="text"
                  [ngModel]="title()"
                  (ngModelChange)="title.set($event)"
                  class="w-full px-3.5 py-2.5 rounded-xl bg-slate-50 dark:bg-surface-800 border border-slate-300 dark:border-slate-700 text-sm font-semibold"
                />
              </div>

              <div class="flex-1 flex flex-col">
                <div class="flex items-center justify-between mb-1.5">
                  <label class="text-xs font-medium text-slate-500 dark:text-slate-400">
                    Bölüm Metni (Bölmek istediğiniz noktaya imleci koyup "İmleçten Böl" düğmesine
                    basabilirsiniz)
                  </label>
                  <div class="flex items-center gap-2">
                    <button
                      type="button"
                      (click)="previewPronunciation()"
                      class="px-3 py-1 rounded-lg bg-slate-200 dark:bg-slate-800 hover:bg-emerald-500/20 text-xs font-semibold text-emerald-400 transition"
                    >
                      🔊 Telaffuz Önizle
                    </button>
                    <button
                      type="button"
                      (click)="splitAtCursor()"
                      [disabled]="saving()"
                      class="px-3 py-1 rounded-lg bg-slate-200 dark:bg-slate-800 hover:bg-brand-500/20 text-xs font-semibold text-brand-400 transition"
                    >
                      ✂ {{ t.bookDetail.splitAtCursor }}
                    </button>
                  </div>
                </div>

                @if (pronunciationPreview(); as prev) {
                  <div class="mb-2 p-3 rounded-xl bg-emerald-500/10 border border-emerald-500/30 text-xs space-y-1">
                    <div class="flex items-center justify-between">
                      <span class="font-semibold text-emerald-300">
                        Telaffuz Önizleme ({{ prev.degisenKelimeSayisi }} kelime dönüştürüldü)
                      </span>
                      <button
                        type="button"
                        (click)="pronunciationPreview.set(null)"
                        class="text-slate-400 hover:text-slate-200"
                      >
                        ✕
                      </button>
                    </div>
                    <p class="text-slate-200 line-clamp-4">{{ prev.onizlemeMetni }}</p>
                  </div>
                }

                <textarea
                  #editorArea
                  [ngModel]="text()"
                  (ngModelChange)="text.set($event)"
                  rows="18"
                  class="flex-1 w-full p-4 rounded-xl bg-slate-50 dark:bg-surface-800 border border-slate-300 dark:border-slate-700 reader-serif text-base leading-relaxed resize-none focus:outline-none focus:ring-2 focus:ring-brand-500"
                ></textarea>
              </div>
            </div>

            <div
              class="px-6 py-4 border-t border-slate-200 dark:border-slate-800 flex items-center justify-end gap-3"
            >
              <button
                type="button"
                (click)="close()"
                class="px-4 py-2.5 min-h-[44px] rounded-xl text-sm font-medium text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800"
              >
                {{ t.common.cancel }}
              </button>
              <button
                type="button"
                (click)="save()"
                [disabled]="saving()"
                class="px-5 py-2.5 min-h-[44px] rounded-xl bg-brand-500 hover:bg-brand-600 disabled:opacity-50 text-slate-950 font-semibold text-sm transition"
              >
                {{ t.bookDetail.saveChapter }}
              </button>
            </div>
          }
        </div>
      </div>
    }
  `
})
export class ChapterEditorComponent {
  private readonly chapterService = inject(ChapterService);
  private readonly pronunciationService = inject(PronunciationService);
  private readonly toast = inject(ToastService);

  @ViewChild('editorArea') editorArea?: ElementRef<HTMLTextAreaElement>;

  readonly chapterId = input<string | null>(null);
  readonly closed = output<void>();
  readonly saved = output<ChapterDetail>();
  readonly chaptersChanged = output<ChapterSummary[]>();

  readonly t = T;
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly conflictDetected = signal(false);
  readonly pronunciationPreview = signal<PronunciationPreviewResponse | null>(null);

  readonly title = signal('');
  readonly text = signal('');
  readonly expectedVersion = signal(1);

  readonly wordCount = computed(() => {
    const raw = this.text().trim();
    return raw ? raw.split(/\s+/).length : 0;
  });

  readonly estimatedSec = computed(() => Math.round((this.wordCount() / 140) * 60));

  constructor() {
    effect(
      () => {
        const id = this.chapterId();
        if (id) {
          this.loadDetail(id);
        }
      },
      { allowSignalWrites: true }
    );
  }

  reloadChapter(): void {
    const id = this.chapterId();
    if (id) {
      this.loadDetail(id);
    }
  }

  private loadDetail(id: string): void {
    this.loading.set(true);
    this.conflictDetected.set(false);
    this.chapterService.getById(id).subscribe({
      next: (det) => {
        this.title.set(det.baslik);
        this.text.set(det.temizMetin);
        this.expectedVersion.set(det.metinSurumu);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }

  save(): void {
    const id = this.chapterId();
    if (!id || this.saving()) return;

    this.saving.set(true);
    this.conflictDetected.set(false);

    this.chapterService
      .updateText(id, {
        baslik: this.title().trim(),
        temizMetin: this.text(),
        beklenenMetinSurumu: this.expectedVersion()
      })
      .subscribe({
        next: (updated) => {
          this.saving.set(false);
          this.expectedVersion.set(updated.metinSurumu);
          this.toast.success('Bölüm metni güncellendi.');
          this.saved.emit(updated);
          this.close();
        },
        error: (err: HttpErrorResponse) => {
          this.saving.set(false);
          if (err.status === 409) {
            this.conflictDetected.set(true);
          } else {
            this.toast.error('Bölüm kaydedilemedi.');
          }
        }
      });
  }

  splitAtCursor(): void {
    const id = this.chapterId();
    const area = this.editorArea?.nativeElement;
    if (!id || !area) return;

    const cursor = area.selectionStart ?? 0;
    if (cursor <= 10 || cursor >= this.text().length - 10) {
      this.toast.warning('Lütfen bölme işlemi için metnin ortasında bir konum seçin.');
      return;
    }

    this.saving.set(true);
    const secondTitle = `${this.title()} (Devam)`;
    this.chapterService.split(id, cursor, secondTitle).subscribe({
      next: (updatedList) => {
        this.saving.set(false);
        this.toast.success('Bölüm ikiye ayrıldı.');
        this.chaptersChanged.emit(updatedList);
        this.close();
      },
      error: () => {
        this.saving.set(false);
      }
    });
  }

  close(): void {
    this.pronunciationPreview.set(null);
    this.closed.emit();
  }

  previewPronunciation(): void {
    const sample = this.text().trim().slice(0, 5000);
    if (!sample) return;
    this.pronunciationService.preview(sample).subscribe({
      next: (res) => this.pronunciationPreview.set(res)
    });
  }
}

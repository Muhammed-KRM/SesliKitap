import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  viewChild
} from '@angular/core';
import { T } from '../../core/i18n/tr';
import { ChapterTimestamps, SentenceTs } from '../../core/models/timestamps.model';
import { AudioFormatService } from '../../core/services/audio-format.service';

export interface ParagraphGroup {
  paragraphIndex: number;
  sentences: SentenceTs[];
}

@Component({
  selector: 'app-sentence-reader',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="relative h-full flex flex-col rounded-2xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-800 overflow-hidden"
    >
      <!-- Üst Araç Çubuğu -->
      <div
        class="px-5 py-3 border-b border-slate-200 dark:border-slate-800 flex items-center justify-between"
      >
        <span class="text-xs font-bold uppercase tracking-wider text-slate-400">
          Canlı Cümle Takibi
        </span>
        <div class="flex items-center gap-1.5">
          <button
            type="button"
            (click)="adjustFont(-2)"
            class="min-w-[36px] min-h-[36px] px-2.5 rounded-lg bg-slate-100 dark:bg-slate-700 hover:bg-slate-200 text-xs font-bold"
            title="Yazı boyutunu küçült"
          >
            A−
          </button>
          <button
            type="button"
            (click)="adjustFont(2)"
            class="min-w-[36px] min-h-[36px] px-2.5 rounded-lg bg-slate-100 dark:bg-slate-700 hover:bg-slate-200 text-xs font-bold"
            title="Yazı boyutunu büyüt"
          >
            A+
          </button>
        </div>
      </div>

      <!-- Cümle Akışı -->
      <div
        #scrollContainer
        (wheel)="onUserManualScroll()"
        (touchmove)="onUserManualScroll()"
        class="flex-1 overflow-y-auto px-6 py-6 space-y-5 font-reader leading-relaxed"
        [style.font-size.px]="formatService.readerFontSize()"
      >
        @if (paragraphGroups().length === 0) {
          <p class="text-sm font-sans text-slate-400 text-center py-12">
            Bu bölüm için cümle zaman damgaları yükleniyor...
          </p>
        } @else {
          @for (group of paragraphGroups(); track group.paragraphIndex) {
            <p
              [class]="
                group.paragraphIndex === -1
                  ? 'font-bold text-xl text-slate-900 dark:text-slate-100 mb-4'
                  : 'text-slate-800 dark:text-slate-200'
              "
            >
              @for (sent of group.sentences; track sent.i) {
                <span
                  [attr.data-sentence-idx]="sent.i"
                  (click)="sentenceClicked.emit(sent.i)"
                  class="cursor-pointer rounded px-1 py-0.5 transition-colors duration-150"
                  [class]="
                    activeSentenceIndex() === sent.i
                      ? 'bg-brand-500/25 text-brand-300 font-semibold ring-1 ring-brand-500/40'
                      : activeSentenceIndex() !== null && sent.i < activeSentenceIndex()!
                        ? 'opacity-55 hover:opacity-90'
                        : 'hover:bg-slate-200/50 dark:hover:bg-slate-700/50'
                  "
                >
                  {{ sent.t }}
                </span>
                <span> </span>
              }
            </p>
          }
        }
      </div>

      <!-- Okunan Yere Dön Düğmesi -->
      @if (userScrolledPause()) {
        <button
          type="button"
          (click)="returnToActiveSentence()"
          class="absolute bottom-4 right-5 px-4 py-2 rounded-full bg-brand-500 hover:bg-brand-600 text-slate-950 font-semibold text-xs shadow-lg transition"
        >
          ↓ {{ t.player.returnToReading }}
        </button>
      }
    </div>
  `
})
export class SentenceReaderComponent {
  readonly formatService = inject(AudioFormatService);

  readonly timestamps = input<ChapterTimestamps | null>(null);
  readonly activeSentenceIndex = input<number | null>(null);
  readonly sentenceClicked = output<number>();

  readonly scrollContainer = viewChild<ElementRef<HTMLElement>>('scrollContainer');
  readonly t = T;

  readonly userScrolledPause = signal(false);
  private pauseTimeoutId: ReturnType<typeof setTimeout> | null = null;

  readonly paragraphGroups = computed<ParagraphGroup[]>(() => {
    const ts = this.timestamps();
    if (!ts?.sentences?.length) return [];

    const all = ts.sentences;
    const activeIdx = this.activeSentenceIndex() ?? 0;

    // 2000+ cümlelik dev bölümlerde pencereleme (aktif cümle ±250)
    const slice =
      all.length > 2000
        ? all.slice(Math.max(0, activeIdx - 250), Math.min(all.length, activeIdx + 250))
        : all;

    const groups: ParagraphGroup[] = [];
    for (const s of slice) {
      const last = groups[groups.length - 1];
      if (!last || last.paragraphIndex !== s.p) {
        groups.push({ paragraphIndex: s.p, sentences: [s] });
      } else {
        last.sentences.push(s);
      }
    }
    return groups;
  });

  constructor() {
    effect(() => {
      const idx = this.activeSentenceIndex();
      if (idx !== null && !this.userScrolledPause()) {
        this.scrollToSentence(idx);
      }
    });
  }

  adjustFont(delta: number): void {
    this.formatService.setReaderFontSize(this.formatService.readerFontSize() + delta);
  }

  onUserManualScroll(): void {
    this.userScrolledPause.set(true);
    if (this.pauseTimeoutId) {
      clearTimeout(this.pauseTimeoutId);
    }
    this.pauseTimeoutId = setTimeout(() => {
      this.userScrolledPause.set(false);
    }, 5000);
  }

  returnToActiveSentence(): void {
    if (this.pauseTimeoutId) {
      clearTimeout(this.pauseTimeoutId);
      this.pauseTimeoutId = null;
    }
    this.userScrolledPause.set(false);
    const idx = this.activeSentenceIndex();
    if (idx !== null) {
      this.scrollToSentence(idx);
    }
  }

  private scrollToSentence(index: number): void {
    const container = this.scrollContainer()?.nativeElement;
    if (!container) return;

    const el = container.querySelector<HTMLElement>(`[data-sentence-idx="${index}"]`);
    if (!el) return;

    const containerRect = container.getBoundingClientRect();
    const elRect = el.getBoundingClientRect();
    const relativeTop = elRect.top - containerRect.top + container.scrollTop;
    const targetScrollTop = Math.max(0, relativeTop - container.clientHeight * 0.35);

    container.scrollTo({
      top: targetScrollTop,
      behavior: 'smooth'
    });
  }
}

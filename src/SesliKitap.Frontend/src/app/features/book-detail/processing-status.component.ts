import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { BookProcessingProgressMsg } from '../../core/services/signalr.service';
import { ProgressBarComponent } from '../../shared/components/progress-bar.component';

const STAGES = [
  { key: 'Indirme', label: 'İndirme' },
  { key: 'MetinCikarma', label: 'Metin Çıkarma' },
  { key: 'Temizleme', label: 'Temizleme' },
  { key: 'BolumTespiti', label: 'Bölüm Tespiti' },
  { key: 'KapakCikarma', label: 'Kapak Çıkarma' },
  { key: 'Tamamlandi', label: 'Tamamlandı' }
];

@Component({
  selector: 'app-processing-status',
  standalone: true,
  imports: [ProgressBarComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="rounded-2xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-800 p-5 space-y-4"
    >
      <div class="flex items-center justify-between gap-2">
        <div>
          <h3 class="text-sm font-semibold text-slate-900 dark:text-slate-100">
            Kitap İşleme Durumu
          </h3>
          <p class="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
            {{ progress()?.mesaj || 'Metin çıkarılıyor ve bölümler ayrıştırılıyor...' }}
          </p>
        </div>
        <span class="text-sm font-bold text-brand-400">%{{ percent() }}</span>
      </div>

      <app-progress-bar [value]="percent()" />

      <div class="grid grid-cols-2 sm:grid-cols-6 gap-2 pt-1">
        @for (stage of stages; track stage.key; let idx = $index) {
          <div
            class="px-2.5 py-2 rounded-xl text-xs font-medium border text-center transition"
            [class]="
              idx < activeStageIndex()
                ? 'bg-emerald-500/10 border-emerald-500/30 text-emerald-400'
                : idx === activeStageIndex()
                  ? 'bg-brand-500/15 border-brand-500/40 text-brand-400 animate-pulse'
                  : 'bg-slate-100 dark:bg-surface-900 border-slate-200 dark:border-slate-800 text-slate-400'
            "
          >
            {{ stage.label }}
          </div>
        }
      </div>

      @if (errorMessage()) {
        <div
          class="p-3 rounded-xl bg-rose-500/10 border border-rose-500/30 text-rose-400 text-xs flex items-center justify-between gap-3"
        >
          <span>{{ errorMessage() }}</span>
          <button
            type="button"
            (click)="retry.emit()"
            class="px-3 py-1.5 rounded-lg bg-rose-500 text-white font-semibold whitespace-nowrap"
          >
            Yeniden İşle
          </button>
        </div>
      }
    </div>
  `
})
export class ProcessingStatusComponent {
  readonly progress = input<BookProcessingProgressMsg | null>(null);
  readonly errorMessage = input<string | null>(null);
  readonly retry = output<void>();

  readonly stages = STAGES;

  readonly percent = computed(() => this.progress()?.yuzde ?? 20);

  readonly activeStageIndex = computed(() => {
    const current = this.progress()?.asama;
    if (!current) return 1;
    const idx = STAGES.findIndex((s) => s.key.toLowerCase() === current.toLowerCase());
    return idx >= 0 ? idx : 1;
  });
}

import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { T } from '../../core/i18n/tr';
import { ChapterSummary } from '../../core/models/chapter.model';
import { StatusBadgeComponent } from '../../shared/components/status-badge.component';
import { DurationPipe } from '../../shared/pipes/duration.pipe';

@Component({
  selector: 'app-chapter-drawer',
  standalone: true,
  imports: [StatusBadgeComponent, DurationPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (open()) {
      <div
        class="fixed inset-0 z-50 bg-slate-950/75 backdrop-blur-sm flex justify-end"
        (click)="closed.emit()"
      >
        <div
          class="w-full max-w-md bg-white dark:bg-surface-900 h-full flex flex-col border-l border-slate-200 dark:border-slate-800 shadow-2xl"
          (click)="$event.stopPropagation()"
        >
          <div
            class="px-5 py-4 border-b border-slate-200 dark:border-slate-800 flex items-center justify-between"
          >
            <h3 class="text-base font-bold text-slate-900 dark:text-slate-100">
              {{ t.player.chapters }} ({{ chapters().length }})
            </h3>
            <button
              type="button"
              (click)="closed.emit()"
              class="min-w-[40px] min-h-[40px] rounded-xl hover:bg-slate-100 dark:hover:bg-slate-800 text-slate-400"
            >
              ✕
            </button>
          </div>

          <div class="flex-1 overflow-y-auto divide-y divide-slate-200 dark:divide-slate-800">
            @for (ch of chapters(); track ch.id) {
              @let playable =
                ch.dahilMi && (ch.sesDurumu === 'Hazir' || ch.sesDurumu === 'GuncelDegil');
              <button
                type="button"
                [disabled]="!playable"
                (click)="selectChapter.emit(ch.id); closed.emit()"
                class="w-full p-4 text-left flex items-center justify-between gap-3 transition disabled:opacity-40 disabled:cursor-not-allowed"
                [class]="
                  currentChapterId() === ch.id
                    ? 'bg-brand-500/15 border-l-4 border-brand-500'
                    : 'hover:bg-slate-50 dark:hover:bg-slate-800/60'
                "
              >
                <div class="min-w-0 flex-1">
                  <p class="text-sm font-semibold text-slate-900 dark:text-slate-100 truncate">
                    #{{ ch.siraNo }} · {{ ch.baslik }}
                  </p>
                  <p class="text-xs text-slate-400 mt-0.5">
                    {{ (ch.sureSn ?? ch.tahminiSureSn) | duration }}
                  </p>
                </div>
                <app-status-badge [status]="ch.sesDurumu" kind="chapter" />
              </button>
            }
          </div>
        </div>
      </div>
    }
  `
})
export class ChapterDrawerComponent {
  readonly open = input<boolean>(false);
  readonly chapters = input<ChapterSummary[]>([]);
  readonly currentChapterId = input<string | null>(null);

  readonly selectChapter = output<string>();
  readonly closed = output<void>();

  readonly t = T;
}

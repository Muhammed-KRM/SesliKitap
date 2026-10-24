import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { T } from '../../core/i18n/tr';

@Component({
  selector: 'app-speed-menu',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="relative">
      <button
        type="button"
        (click)="open.set(!open())"
        class="min-h-[44px] px-3.5 py-2 rounded-xl bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-xs font-bold text-slate-800 dark:text-slate-100 flex items-center gap-1.5 transition"
        [title]="t.player.speed"
      >
        <span>⚡ {{ rate() }}x</span>
      </button>

      @if (open()) {
        <div
          class="absolute bottom-14 left-0 z-30 w-64 p-4 rounded-2xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-700 shadow-2xl space-y-3"
        >
          <div class="flex items-center justify-between">
            <span class="text-xs font-bold text-slate-900 dark:text-slate-100">
              {{ t.player.speed }}
            </span>
            <span class="text-xs font-mono text-brand-400 font-bold">{{ rate() }}x</span>
          </div>

          <input
            type="range"
            min="0.5"
            max="3.0"
            step="0.05"
            [ngModel]="rate()"
            (ngModelChange)="selectRate($event)"
            class="w-full accent-brand-500"
          />

          <div class="grid grid-cols-4 gap-1.5">
            @for (preset of presets; track preset) {
              <button
                type="button"
                (click)="selectRate(preset); open.set(false)"
                class="py-1.5 rounded-lg text-xs font-semibold transition"
                [class]="
                  rate() === preset
                    ? 'bg-brand-500 text-slate-950'
                    : 'bg-slate-100 dark:bg-slate-700 text-slate-700 dark:text-slate-200 hover:bg-slate-200'
                "
              >
                {{ preset }}x
              </button>
            }
          </div>
        </div>
      }
    </div>
  `
})
export class SpeedMenuComponent {
  readonly rate = input<number>(1.0);
  readonly rateChange = output<number>();

  readonly t = T;
  readonly open = signal(false);
  readonly presets = [0.75, 1.0, 1.25, 1.5, 1.75, 2.0, 2.5, 3.0];

  selectRate(val: number): void {
    this.rateChange.emit(Number(val));
  }
}

import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { T } from '../../core/i18n/tr';
import { SleepTimerService } from '../../core/services/sleep-timer.service';
import { ClockPipe } from '../../shared/pipes/clock.pipe';

@Component({
  selector: 'app-sleep-timer-menu',
  standalone: true,
  imports: [ClockPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="relative">
      <button
        type="button"
        (click)="open.set(!open())"
        class="min-h-[44px] px-3.5 py-2 rounded-xl text-xs font-semibold flex items-center gap-1.5 transition"
        [class]="
          sleepTimer.isActive()
            ? 'bg-brand-500/20 text-brand-400 border border-brand-500/40'
            : 'bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-700 dark:text-slate-200'
        "
        [title]="t.player.sleepTimer"
      >
        <span>🌙</span>
        @if (sleepTimer.mode() === 'dakika') {
          <span class="font-mono">{{ sleepTimer.remainingSec() | clock }}</span>
        } @else if (sleepTimer.mode() === 'bolumSonu') {
          <span>Bölüm Sonu</span>
        } @else {
          <span>Uyku</span>
        }
      </button>

      @if (open()) {
        <div
          class="absolute bottom-14 left-0 z-30 w-56 p-3 rounded-2xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-700 shadow-2xl space-y-1.5"
        >
          <p class="px-2 py-1 text-xs font-bold text-slate-400 uppercase tracking-wider">
            {{ t.player.sleepTimer }}
          </p>

          @for (min of minuteOptions; track min) {
            <button
              type="button"
              (click)="startMinutes(min)"
              class="w-full text-left px-3 py-2 rounded-xl text-xs font-medium hover:bg-slate-100 dark:hover:bg-slate-700 transition"
            >
              {{ min }} dakika sonra durdur
            </button>
          }

          <button
            type="button"
            (click)="startEndOfChapter()"
            class="w-full text-left px-3 py-2 rounded-xl text-xs font-medium hover:bg-slate-100 dark:hover:bg-slate-700 transition"
          >
            {{ t.player.sleepEndOfChapter }}
          </button>

          @if (sleepTimer.isActive()) {
            <button
              type="button"
              (click)="cancelTimer()"
              class="w-full text-left px-3 py-2 rounded-xl text-xs font-semibold text-rose-400 hover:bg-rose-500/10 transition"
            >
              {{ t.player.sleepOff }}
            </button>
          }
        </div>
      }
    </div>
  `
})
export class SleepTimerMenuComponent {
  readonly sleepTimer = inject(SleepTimerService);
  readonly t = T;
  readonly open = signal(false);
  readonly minuteOptions = [5, 15, 30, 45, 60];

  startMinutes(min: number): void {
    this.sleepTimer.startMinutes(min);
    this.open.set(false);
  }

  startEndOfChapter(): void {
    this.sleepTimer.startEndOfChapter();
    this.open.set(false);
  }

  cancelTimer(): void {
    this.sleepTimer.cancel();
    this.open.set(false);
  }
}

import { Injectable, computed, signal } from '@angular/core';

export type SleepTimerMode = 'kapali' | 'dakika' | 'bolumSonu';

@Injectable({ providedIn: 'root' })
export class SleepTimerService {
  readonly mode = signal<SleepTimerMode>('kapali');
  readonly remainingSec = signal<number>(0);

  readonly isActive = computed(() => this.mode() !== 'kapali');

  private timerId: ReturnType<typeof setInterval> | null = null;
  private onVolumeChangeCallback: ((volume: number) => void) | null = null;
  private onPauseCallback: (() => void) | null = null;

  registerCallbacks(opts: {
    onVolumeChange: (volume: number) => void;
    onPause: () => void;
  }): void {
    this.onVolumeChangeCallback = opts.onVolumeChange;
    this.onPauseCallback = opts.onPause;
  }

  startMinutes(minutes: number): void {
    this.clearIntervalOnly();
    const totalSec = Math.max(1, Math.round(minutes * 60));
    this.mode.set('dakika');
    this.remainingSec.set(totalSec);
    this.onVolumeChangeCallback?.(1.0);

    this.timerId = setInterval(() => {
      const next = this.remainingSec() - 1;
      if (next <= 0) {
        this.clearIntervalOnly();
        this.mode.set('kapali');
        this.remainingSec.set(0);
        this.onPauseCallback?.();
        this.onVolumeChangeCallback?.(1.0);
        return;
      }

      this.remainingSec.set(next);
      // Son 10 saniyede sesi kademeli olarak 1.0 -> 0.1 düşür
      if (next <= 10) {
        this.onVolumeChangeCallback?.(Math.max(0.05, next / 10));
      }
    }, 1000);
  }

  startEndOfChapter(): void {
    this.clearIntervalOnly();
    this.mode.set('bolumSonu');
    this.remainingSec.set(0);
    this.onVolumeChangeCallback?.(1.0);
  }

  cancel(): void {
    this.clearIntervalOnly();
    this.mode.set('kapali');
    this.remainingSec.set(0);
    this.onVolumeChangeCallback?.(1.0);
  }

  /**
   * Bölüm bittiğinde AudioPlayerService tarafından çağrılır.
   * Eğer 'bolumSonu' modundaysa zamanlayıcıyı sıfırlar ve true döner (sonraki bölüme geçilmez).
   */
  consumeEndOfChapterStop(): boolean {
    if (this.mode() === 'bolumSonu') {
      this.cancel();
      return true;
    }
    return false;
  }

  private clearIntervalOnly(): void {
    if (this.timerId !== null) {
      clearInterval(this.timerId);
      this.timerId = null;
    }
  }
}

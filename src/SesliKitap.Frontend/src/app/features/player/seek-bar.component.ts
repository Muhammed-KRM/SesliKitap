import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
  signal
} from '@angular/core';
import { ClockPipe } from '../../shared/pipes/clock.pipe';

@Component({
  selector: 'app-seek-bar',
  standalone: true,
  imports: [ClockPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-1.5 select-none">
      <div
        role="slider"
        tabindex="0"
        aria-label="Ses konumu kaydırıcısı"
        [attr.aria-valuemin]="0"
        [attr.aria-valuemax]="duration()"
        [attr.aria-valuenow]="displayPosition()"
        (keydown)="onKeyDown($event)"
        class="relative h-6 flex items-center cursor-pointer group"
      >
        <!-- Arka Plan İzi -->
        <div class="w-full h-2 rounded-full bg-slate-200 dark:bg-slate-800 overflow-hidden relative">
          <!-- Tamponlanan (Buffered) Kısım -->
          <div
            class="absolute inset-y-0 left-0 bg-slate-300 dark:bg-slate-700 transition-all"
            [style.width.%]="bufferedPercent()"
          ></div>
          <!-- İlerleme Kısmı -->
          <div
            class="absolute inset-y-0 left-0 bg-brand-500 transition-all"
            [style.width.%]="progressPercent()"
          ></div>
        </div>

        <!-- Yerel Range Input (pointerdown/move/up ile bırakınca seek yapar) -->
        <input
          type="range"
          min="0"
          [max]="duration() || 100"
          step="0.5"
          [value]="displayPosition()"
          (pointerdown)="onDragStart()"
          (input)="onDragInput($event)"
          (change)="onDragEnd($event)"
          class="absolute inset-0 w-full h-full opacity-0 cursor-pointer"
        />
      </div>

      <div class="flex items-center justify-between text-xs font-mono text-slate-400">
        <span>{{ displayPosition() | clock }}</span>
        <span>
          -{{ adjustedRemainingSec() | clock }}
          @if (playbackRate() !== 1) {
            <span class="text-[10px] text-brand-400 ml-1">({{ playbackRate() }}x)</span>
          }
        </span>
      </div>
    </div>
  `
})
export class SeekBarComponent {
  readonly position = input<number>(0);
  readonly duration = input<number>(0);
  readonly bufferedEnd = input<number>(0);
  readonly playbackRate = input<number>(1.0);

  readonly seekChange = output<number>();

  readonly isDragging = signal(false);
  readonly dragValue = signal(0);

  readonly displayPosition = computed(() =>
    this.isDragging() ? this.dragValue() : this.position()
  );

  readonly progressPercent = computed(() => {
    const dur = this.duration();
    if (dur <= 0) return 0;
    return Math.min(100, Math.max(0, (this.displayPosition() / dur) * 100));
  });

  readonly bufferedPercent = computed(() => {
    const dur = this.duration();
    if (dur <= 0) return 0;
    return Math.min(100, Math.max(0, (this.bufferedEnd() / dur) * 100));
  });

  readonly adjustedRemainingSec = computed(() => {
    const rem = Math.max(0, this.duration() - this.displayPosition());
    const rate = Math.max(0.5, this.playbackRate());
    return rem / rate;
  });

  onDragStart(): void {
    this.isDragging.set(true);
    this.dragValue.set(this.position());
  }

  onDragInput(event: Event): void {
    const val = Number((event.target as HTMLInputElement).value);
    this.dragValue.set(val);
  }

  onDragEnd(event: Event): void {
    const val = Number((event.target as HTMLInputElement).value);
    this.isDragging.set(false);
    this.seekChange.emit(val);
  }

  onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'ArrowRight') {
      event.preventDefault();
      this.seekChange.emit(Math.min(this.duration(), this.position() + 5));
    } else if (event.key === 'ArrowLeft') {
      event.preventDefault();
      this.seekChange.emit(Math.max(0, this.position() - 5));
    }
  }
}

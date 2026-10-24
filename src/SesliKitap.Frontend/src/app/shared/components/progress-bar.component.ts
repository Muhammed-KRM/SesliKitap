import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

@Component({
  selector: 'app-progress-bar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="w-full h-2 bg-slate-200 dark:bg-slate-800 rounded-full overflow-hidden"
      role="progressbar"
      [attr.aria-valuenow]="clamped()"
      aria-valuemin="0"
      aria-valuemax="100"
    >
      @if (indeterminate()) {
        <div class="h-full w-1/3 bg-brand-500 rounded-full animate-pulse"></div>
      } @else {
        <div
          class="h-full transition-all duration-300 rounded-full"
          [class]="barColorClass()"
          [style.width.%]="clamped()"
        ></div>
      }
    </div>
  `
})
export class ProgressBarComponent {
  readonly value = input<number>(0);
  readonly indeterminate = input<boolean>(false);
  readonly variant = input<'brand' | 'emerald' | 'rose'>('brand');

  readonly clamped = computed(() => Math.max(0, Math.min(100, Math.round(this.value() ?? 0))));

  readonly barColorClass = computed(() => {
    switch (this.variant()) {
      case 'emerald':
        return 'bg-emerald-500';
      case 'rose':
        return 'bg-rose-500';
      default:
        return 'bg-brand-500';
    }
  });
}

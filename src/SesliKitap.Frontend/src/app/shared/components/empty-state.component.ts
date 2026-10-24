import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

@Component({
  selector: 'app-empty-state',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="flex flex-col items-center justify-center text-center py-12 px-6 rounded-2xl border border-dashed border-slate-300 dark:border-slate-800 bg-white/50 dark:bg-surface-800/40"
    >
      <div
        class="w-12 h-12 rounded-full bg-brand-500/15 text-brand-500 flex items-center justify-center mb-4 text-xl font-bold"
      >
        ♪
      </div>
      <h3 class="text-base font-semibold text-slate-900 dark:text-slate-100">
        {{ title() }}
      </h3>
      @if (description()) {
        <p class="mt-1.5 text-sm text-slate-500 dark:text-slate-400 max-w-md">
          {{ description() }}
        </p>
      }
      @if (actionLabel()) {
        <button
          type="button"
          (click)="actionClick.emit()"
          class="mt-5 inline-flex items-center gap-2 px-4 py-2.5 rounded-xl bg-brand-500 hover:bg-brand-600 text-slate-950 font-semibold text-sm transition"
        >
          {{ actionLabel() }}
        </button>
      }
    </div>
  `
})
export class EmptyStateComponent {
  readonly title = input.required<string>();
  readonly description = input<string>('');
  readonly actionLabel = input<string>('');
  readonly actionClick = output<void>();
}

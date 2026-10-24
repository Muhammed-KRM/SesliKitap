import { ChangeDetectionStrategy, Component, Injectable, inject, signal } from '@angular/core';
import { T } from '../../core/i18n/tr';

export interface ConfirmOptions {
  title: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
  danger?: boolean;
}

interface ActiveConfirm extends ConfirmOptions {
  resolve: (result: boolean) => void;
}

@Injectable({ providedIn: 'root' })
export class ConfirmService {
  readonly active = signal<ActiveConfirm | null>(null);

  ask(options: ConfirmOptions): Promise<boolean> {
    return new Promise<boolean>((resolve) => {
      this.active.set({ ...options, resolve });
    });
  }

  respond(result: boolean): void {
    const current = this.active();
    if (current) {
      this.active.set(null);
      current.resolve(result);
    }
  }
}

@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (confirm.active(); as modal) {
      <div
        class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-xs p-4"
        role="dialog"
        aria-modal="true"
      >
        <div
          class="w-full max-w-md rounded-2xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-700 p-6 shadow-2xl"
        >
          <h3 class="text-lg font-semibold text-slate-900 dark:text-slate-100">
            {{ modal.title }}
          </h3>
          <p class="mt-2 text-sm text-slate-600 dark:text-slate-300">
            {{ modal.message }}
          </p>
          <div class="mt-6 flex items-center justify-end gap-3">
            <button
              type="button"
              (click)="confirm.respond(false)"
              class="px-4 py-2.5 min-h-[44px] rounded-xl text-sm font-medium text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-700/60 transition"
            >
              {{ modal.cancelText || t.common.cancel }}
            </button>
            <button
              type="button"
              (click)="confirm.respond(true)"
              class="px-4 py-2.5 min-h-[44px] rounded-xl text-sm font-semibold transition"
              [class]="
                modal.danger
                  ? 'bg-rose-600 hover:bg-rose-500 text-white'
                  : 'bg-brand-500 hover:bg-brand-600 text-slate-950'
              "
            >
              {{ modal.confirmText || t.common.confirm }}
            </button>
          </div>
        </div>
      </div>
    }
  `
})
export class ConfirmDialogComponent {
  readonly confirm = inject(ConfirmService);
  readonly t = T;
}

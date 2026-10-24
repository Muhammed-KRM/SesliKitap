import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ToastService } from '../../core/services/toast.service';

@Component({
  selector: 'app-toast-container',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="fixed top-16 right-4 z-50 flex flex-col gap-2.5 max-w-sm w-full pointer-events-none"
      aria-live="polite"
    >
      @for (item of toast.toasts(); track item.id) {
        <div
          class="pointer-events-auto flex items-start gap-3 p-4 rounded-xl shadow-lg border backdrop-blur-md transition"
          [class]="cardClasses(item.type)"
        >
          <div class="flex-1 min-w-0">
            <p class="text-sm font-semibold">{{ item.title }}</p>
            @if (item.detail) {
              <p class="mt-0.5 text-xs opacity-90 break-words">{{ item.detail }}</p>
            }
            @if (item.action) {
              <button
                type="button"
                (click)="item.action.callback(); toast.dismiss(item.id)"
                class="mt-2 inline-flex items-center px-2.5 py-1 rounded-lg text-xs font-semibold bg-white/15 hover:bg-white/25 transition"
              >
                {{ item.action.label }}
              </button>
            }
          </div>
          <button
            type="button"
            (click)="toast.dismiss(item.id)"
            class="text-xs opacity-70 hover:opacity-100 p-1"
            aria-label="Kapat"
          >
            ✕
          </button>
        </div>
      }
    </div>
  `
})
export class ToastContainerComponent {
  readonly toast = inject(ToastService);

  cardClasses(type: string): string {
    switch (type) {
      case 'basari':
        return 'bg-emerald-950/90 text-emerald-100 border-emerald-500/40';
      case 'hata':
        return 'bg-rose-950/90 text-rose-100 border-rose-500/40';
      case 'uyari':
        return 'bg-amber-950/90 text-amber-100 border-amber-500/40';
      default:
        return 'bg-slate-900/90 text-slate-100 border-slate-700';
    }
  }
}

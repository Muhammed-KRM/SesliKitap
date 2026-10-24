import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { T } from '../../core/i18n/tr';
import { AccessKeyService } from '../../core/services/access-key.service';
import { SignalrService } from '../../core/services/signalr.service';

@Component({
  selector: 'app-access-page',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-[calc(100vh-4rem)] flex items-center justify-center p-4">
      <div
        class="w-full max-w-md rounded-2xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-800 p-6 sm:p-8 shadow-xl"
      >
        <div class="flex flex-col items-center text-center">
          <div
            class="w-14 h-14 rounded-2xl bg-brand-500 text-slate-950 flex items-center justify-center font-serif font-extrabold text-2xl mb-4 shadow-md"
          >
            S
          </div>
          <h1 class="text-xl font-bold text-slate-900 dark:text-slate-100">
            {{ t.access.title }}
          </h1>
          <p class="mt-1.5 text-sm text-slate-500 dark:text-slate-400">
            {{ t.access.subtitle }}
          </p>
        </div>

        <form (ngSubmit)="onSubmit()" class="mt-6 space-y-4">
          <div>
            <label
              for="apiKeyInput"
              class="block text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1.5"
            >
              {{ t.access.title }}
            </label>
            <input
              id="apiKeyInput"
              type="password"
              name="apiKey"
              [(ngModel)]="apiKeyInput"
              [placeholder]="t.access.placeholder"
              autocomplete="current-password"
              class="w-full px-4 py-3 rounded-xl bg-slate-50 dark:bg-surface-900 border border-slate-300 dark:border-slate-700 text-slate-900 dark:text-slate-100 focus:outline-none focus:ring-2 focus:ring-brand-500 text-sm"
            />
          </div>

          @if (errorMsg()) {
            <div
              class="p-3 rounded-xl bg-rose-500/10 border border-rose-500/30 text-rose-400 text-xs font-medium"
            >
              {{ errorMsg() }}
            </div>
          }

          <button
            type="submit"
            [disabled]="loading()"
            class="w-full min-h-[44px] py-3 px-4 rounded-xl bg-brand-500 hover:bg-brand-600 disabled:opacity-50 text-slate-950 font-semibold text-sm transition"
          >
            {{ loading() ? t.access.verifying : t.access.connect }}
          </button>
        </form>
      </div>
    </div>
  `
})
export class AccessPageComponent {
  private readonly accessKey = inject(AccessKeyService);
  private readonly signalr = inject(SignalrService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly t = T;
  apiKeyInput = '';
  readonly loading = signal(false);
  readonly errorMsg = signal<string | null>(null);

  onSubmit(): void {
    const candidate = this.apiKeyInput.trim();
    if (!candidate) {
      this.errorMsg.set(T.access.required);
      return;
    }

    this.loading.set(true);
    this.errorMsg.set(null);

    this.accessKey.verify(candidate).subscribe((valid) => {
      this.loading.set(false);
      if (!valid) {
        this.errorMsg.set(T.access.invalid);
        return;
      }

      void this.signalr.connect();
      const returnUrl = this.route.snapshot.queryParamMap.get('donus') || '/';
      void this.router.navigateByUrl(returnUrl);
    });
  }
}

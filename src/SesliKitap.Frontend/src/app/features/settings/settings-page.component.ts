import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { environment } from '../../../environments/environment';
import { T } from '../../core/i18n/tr';
import { AccessKeyService } from '../../core/services/access-key.service';
import { DeviceService } from '../../core/services/device.service';
import { SignalrService } from '../../core/services/signalr.service';
import { ThemeService } from '../../core/services/theme.service';
import { ToastService } from '../../core/services/toast.service';

@Component({
  selector: 'app-settings-page',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="max-w-4xl mx-auto px-4 md:px-6 py-6 space-y-6">
      <h1 class="text-2xl font-bold text-slate-900 dark:text-slate-100">
        {{ t.settings.title }}
      </h1>

      <!-- Görünüm ve Cihaz -->
      <section
        class="rounded-2xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-800 p-6 space-y-4"
      >
        <h2 class="text-base font-bold text-slate-900 dark:text-slate-100">
          Görünüm ve Oynatma Tercihleri
        </h2>

        <div class="flex items-center justify-between">
          <span class="text-sm font-medium">{{ t.settings.theme }}</span>
          <button
            type="button"
            (click)="theme.toggle()"
            class="px-4 py-2 min-h-[40px] rounded-xl bg-slate-100 dark:bg-slate-700 text-sm font-semibold"
          >
            {{ theme.mode() === 'dark' ? t.settings.dark : t.settings.light }}
          </button>
        </div>

        <div class="grid grid-cols-1 sm:grid-cols-2 gap-4 pt-2">
          <div>
            <label class="block text-xs font-medium text-slate-500 dark:text-slate-400 mb-1">
              {{ t.settings.deviceName }}
            </label>
            <div class="flex gap-2">
              <input
                type="text"
                [(ngModel)]="deviceNameInput"
                class="flex-1 px-3.5 py-2 rounded-xl bg-slate-50 dark:bg-surface-900 border border-slate-300 dark:border-slate-700 text-sm"
              />
              <button
                type="button"
                (click)="saveDeviceName()"
                class="px-3.5 py-2 rounded-xl bg-brand-500 text-slate-950 text-xs font-semibold"
              >
                {{ t.common.save }}
              </button>
            </div>
          </div>
        </div>
      </section>

      <!-- Sistem Sağlığı -->
      <section
        class="rounded-2xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-800 p-6 space-y-4"
      >
        <div class="flex items-center justify-between">
          <h2 class="text-base font-bold text-slate-900 dark:text-slate-100">
            {{ t.settings.healthTitle }}
          </h2>
          <button
            type="button"
            (click)="loadHealth()"
            class="text-xs font-semibold text-brand-400 hover:underline"
          >
            Yenile
          </button>
        </div>

        @if (healthReport(); as h) {
          <div class="grid grid-cols-2 sm:grid-cols-3 gap-3 text-xs">
            @for (entry of healthEntries(h); track entry.key) {
              <div class="p-3 rounded-xl bg-slate-50 dark:bg-surface-900 flex items-center justify-between">
                <span class="font-medium text-slate-400">{{ entry.key }}</span>
                <span class="font-bold text-emerald-400">{{ entry.val }}</span>
              </div>
            }
          </div>
        } @else {
          <p class="text-xs text-slate-400">Sağlık bilgisi yükleniyor...</p>
        }
      </section>

      <!-- Erişim ve Çıkış -->
      <section
        class="rounded-2xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-800 p-6 flex items-center justify-between"
      >
        <div>
          <h2 class="text-sm font-bold text-slate-900 dark:text-slate-100">
            Oturum ve Erişim Anahtarı
          </h2>
          <p class="text-xs text-slate-500 dark:text-slate-400">
            Bu tarayıcıda kayıtlı API erişim anahtarını temizler.
          </p>
        </div>
        <button
          type="button"
          (click)="logout()"
          class="px-4 py-2.5 min-h-[44px] rounded-xl bg-rose-600 hover:bg-rose-500 text-white text-xs font-semibold transition"
        >
          {{ t.settings.logout }}
        </button>
      </section>
    </div>
  `
})
export class SettingsPageComponent implements OnInit {
  readonly theme = inject(ThemeService);
  private readonly device = inject(DeviceService);
  private readonly accessKey = inject(AccessKeyService);
  private readonly signalr = inject(SignalrService);
  private readonly toast = inject(ToastService);
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  readonly t = T;
  deviceNameInput = this.device.deviceName();
  readonly healthReport = signal<Record<string, unknown> | null>(null);

  ngOnInit(): void {
    this.loadHealth();
  }

  saveDeviceName(): void {
    this.device.setDeviceName(this.deviceNameInput);
    this.toast.success('Cihaz adı kaydedildi.');
  }

  loadHealth(): void {
    this.http
      .get<Record<string, unknown>>(`${environment.apiUrl}/health`)
      .subscribe({
        next: (rep) => this.healthReport.set(rep),
        error: () => this.healthReport.set(null)
      });
  }

  healthEntries(obj: Record<string, unknown>): { key: string; val: string }[] {
    const bilesenler = (obj['bilesenler'] as Record<string, { durum?: string }>) ?? {};
    if (Object.keys(bilesenler).length > 0) {
      return Object.entries(bilesenler).map(([k, v]) => ({
        key: k,
        val: v?.durum ?? 'Hazır'
      }));
    }
    return [{ key: 'Genel Durum', val: String(obj['durum'] ?? obj['status'] ?? 'Healthy') }];
  }

  logout(): void {
    this.accessKey.clear();
    void this.signalr.disconnect();
    void this.router.navigate(['/erisim']);
  }
}

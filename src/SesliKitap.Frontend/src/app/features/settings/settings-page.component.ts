import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { environment } from '../../../environments/environment';
import { T } from '../../core/i18n/tr';
import { AccessKeyService } from '../../core/services/access-key.service';
import {
  AudioFormatPreference,
  AudioFormatService
} from '../../core/services/audio-format.service';
import { DeviceService } from '../../core/services/device.service';
import { OfflineStorageService } from '../../core/services/offline-storage.service';
import { SignalrService } from '../../core/services/signalr.service';
import { ThemeService } from '../../core/services/theme.service';
import { ToastService } from '../../core/services/toast.service';
import { ConfirmService } from '../../shared/components/confirm-dialog.component';
import { FileSizePipe } from '../../shared/pipes/file-size.pipe';

@Component({
  selector: 'app-settings-page',
  standalone: true,
  imports: [FormsModule, FileSizePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="max-w-4xl mx-auto px-4 md:px-6 py-6 space-y-6">
      <h1 class="text-2xl font-bold text-slate-900 dark:text-slate-100">
        {{ t.settings.title }}
      </h1>

      <!-- Görünüm ve Oynatma Tercihleri -->
      <section
        class="rounded-2xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-800 p-6 space-y-5"
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
              {{ t.settings.audioFormat }}
            </label>
            <select
              [ngModel]="formatService.preference()"
              (ngModelChange)="onFormatChange($event)"
              class="w-full px-3.5 py-2.5 rounded-xl bg-slate-50 dark:bg-surface-900 border border-slate-300 dark:border-slate-700 text-sm"
            >
              <option value="auto">{{ t.settings.formatAuto }}</option>
              <option value="opus">{{ t.settings.formatOpus }}</option>
              <option value="mp3">{{ t.settings.formatMp3 }}</option>
            </select>
          </div>

          <div>
            <label class="block text-xs font-medium text-slate-500 dark:text-slate-400 mb-1">
              {{ t.settings.defaultSpeed }} ({{ formatService.defaultRate() }}x)
            </label>
            <input
              type="range"
              min="0.5"
              max="3.0"
              step="0.05"
              [ngModel]="formatService.defaultRate()"
              (ngModelChange)="formatService.setDefaultRate($event)"
              class="w-full accent-brand-500 mt-2"
            />
          </div>

          <div>
            <label class="block text-xs font-medium text-slate-500 dark:text-slate-400 mb-1">
              Geri / İleri Sarma Süreleri (saniye)
            </label>
            <div class="flex items-center gap-2">
              <select
                [ngModel]="formatService.skipBackSec()"
                (ngModelChange)="formatService.setSkipIntervals(+$event, formatService.skipForwardSec())"
                class="flex-1 px-3 py-2 rounded-xl bg-slate-50 dark:bg-surface-900 border border-slate-300 dark:border-slate-700 text-sm"
              >
                <option [value]="10">Geri 10 sn</option>
                <option [value]="15">Geri 15 sn</option>
                <option [value]="30">Geri 30 sn</option>
              </select>
              <select
                [ngModel]="formatService.skipForwardSec()"
                (ngModelChange)="formatService.setSkipIntervals(formatService.skipBackSec(), +$event)"
                class="flex-1 px-3 py-2 rounded-xl bg-slate-50 dark:bg-surface-900 border border-slate-300 dark:border-slate-700 text-sm"
              >
                <option [value]="15">İleri 15 sn</option>
                <option [value]="30">İleri 30 sn</option>
                <option [value]="60">İleri 60 sn</option>
              </select>
            </div>
          </div>

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

      <!-- Çevrimdışı Depolama -->
      <section
        class="rounded-2xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-800 p-6 flex flex-wrap items-center justify-between gap-4"
      >
        <div>
          <h2 class="text-sm font-bold text-slate-900 dark:text-slate-100">
            Çevrimdışı Depolama Kullanımı
          </h2>
          <p class="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
            Kullanılan: <strong>{{ usedBytes() | fileSize }}</strong>
            @if (quotaBytes() > 0) {
              <span> / Kota: {{ quotaBytes() | fileSize }}</span>
            }
          </p>
        </div>
        <button
          type="button"
          (click)="clearOfflineStorage()"
          class="px-4 py-2 min-h-[40px] rounded-xl border border-rose-500/30 text-rose-400 hover:bg-rose-500/10 text-xs font-semibold transition"
        >
          Tüm Çevrimdışı Verileri Sil
        </button>
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
              <div
                class="p-3 rounded-xl bg-slate-50 dark:bg-surface-900 flex items-center justify-between"
              >
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
  readonly formatService = inject(AudioFormatService);
  private readonly device = inject(DeviceService);
  private readonly offlineStorage = inject(OfflineStorageService);
  private readonly confirm = inject(ConfirmService);
  private readonly accessKey = inject(AccessKeyService);
  private readonly signalr = inject(SignalrService);
  private readonly toast = inject(ToastService);
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  readonly t = T;
  deviceNameInput = this.device.deviceName();
  readonly healthReport = signal<Record<string, unknown> | null>(null);
  readonly usedBytes = signal(0);
  readonly quotaBytes = signal(0);

  ngOnInit(): void {
    this.loadHealth();
    void this.loadStorageUsage();
  }

  async loadStorageUsage(): Promise<void> {
    const est = await this.offlineStorage.usage();
    this.usedBytes.set(est.used);
    this.quotaBytes.set(est.quota);
  }

  onFormatChange(pref: AudioFormatPreference): void {
    this.formatService.setPreference(pref);
    this.toast.success('Ses formatı tercihi güncellendi.');
  }

  saveDeviceName(): void {
    this.device.setDeviceName(this.deviceNameInput);
    this.toast.success('Cihaz adı kaydedildi.');
  }

  async clearOfflineStorage(): Promise<void> {
    const ok = await this.confirm.ask({
      title: 'Çevrimdışı Depolamayı Temizle',
      message: 'Cihazdaki tüm indirilen kitaplar silinecek. Emin misiniz?',
      danger: true
    });
    if (!ok) return;

    await this.offlineStorage.deleteAll();
    this.toast.success('Çevrimdışı depolama temizlendi.');
    await this.loadStorageUsage();
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

import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { MonthlyUsageDto } from '../../core/models/faz8.model';
import { UsageService } from '../../core/services/usage.service';
import { DurationPipe } from '../../shared/pipes/duration.pipe';

@Component({
  selector: 'app-usage-panel',
  standalone: true,
  imports: [DurationPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section
      class="rounded-2xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-800 p-6 space-y-5"
    >
      <div class="flex items-center justify-between">
        <div>
          <h2 class="text-base font-bold text-slate-900 dark:text-slate-100">
            Aylık GPU Kullanımı ve Maliyet Raporu
          </h2>
          <p class="text-xs text-slate-500 dark:text-slate-400">
            Modal.com sunucusuz GPU seslendirme süresi ve aylık bütçe takibi
          </p>
        </div>
        <button
          type="button"
          (click)="load()"
          class="text-xs font-semibold text-brand-400 hover:underline"
        >
          Yenile
        </button>
      </div>

      @if (usage(); as u) {
        <div class="grid grid-cols-2 sm:grid-cols-4 gap-3">
          <div class="p-3.5 rounded-xl bg-slate-50 dark:bg-surface-900">
            <span class="text-xs text-slate-400 block">Dönem ({{ u.ay }})</span>
            <span class="text-lg font-bold text-slate-900 dark:text-slate-100">
              {{ u.toplamGpuSaat.toFixed(2) }} sa
            </span>
            <span class="text-[11px] text-slate-400 block">{{ u.toplamGpuSn }} sn GPU</span>
          </div>

          <div class="p-3.5 rounded-xl bg-slate-50 dark:bg-surface-900">
            <span class="text-xs text-slate-400 block">Tahmini Maliyet</span>
            <span
              class="text-lg font-bold"
              [class]="u.limitAsildiMi ? 'text-rose-400' : 'text-emerald-400'"
            >
              \${{ u.tahminiMaliyetUsd.toFixed(2) }}
            </span>
            <span class="text-[11px] text-slate-400 block">
              Limit: \${{ u.aylikLimitUsd.toFixed(2) }}
            </span>
          </div>

          <div class="p-3.5 rounded-xl bg-slate-50 dark:bg-surface-900">
            <span class="text-xs text-slate-400 block">Seslendirilen</span>
            <span class="text-lg font-bold text-slate-900 dark:text-slate-100">
              {{ u.seslendirilenKitapSayisi }} kitap
            </span>
            <span class="text-[11px] text-slate-400 block">
              {{ u.seslendirilenBolumSayisi }} bölüm
            </span>
          </div>

          <div class="p-3.5 rounded-xl bg-slate-50 dark:bg-surface-900">
            <span class="text-xs text-slate-400 block">Üretilen Ses Süresi</span>
            <span class="text-lg font-bold text-brand-400">
              {{ u.toplamSesSuresiSn | duration }}
            </span>
            <span class="text-[11px] text-slate-400 block">Toplam ses çıktısı</span>
          </div>
        </div>

        <!-- Bütçe İlerleme Çubuğu -->
        <div class="space-y-1.5">
          <div class="flex items-center justify-between text-xs">
            <span class="font-medium text-slate-500 dark:text-slate-400">Aylık Bütçe Kullanımı</span>
            <span class="font-semibold" [class]="u.limitAsildiMi ? 'text-rose-400' : 'text-slate-300'">
              %{{ budgetPercent() }}
            </span>
          </div>
          <div class="h-2.5 rounded-full bg-slate-200 dark:bg-surface-900 overflow-hidden">
            <div
              class="h-full rounded-full transition-all"
              [class]="u.limitAsildiMi ? 'bg-rose-500' : 'bg-brand-500'"
              [style.width.%]="Math.min(100, budgetPercent())"
            ></div>
          </div>
        </div>

        <!-- Son 6 Ay SVG Sütun Grafiği -->
        @if (u.sonAltiAyGecmisi.length > 0) {
          <div class="pt-2 space-y-2">
            <h3 class="text-xs font-semibold text-slate-400">Son 6 Ayın Harcama Geçmişi (USD)</h3>
            <div class="p-4 rounded-xl bg-slate-50 dark:bg-surface-900">
              <svg viewBox="0 0 360 110" class="w-full h-28 overflow-visible" role="img" aria-label="Son 6 ay GPU maliyeti grafiği">
                @for (item of u.sonAltiAyGecmisi; track item.ay; let idx = $index) {
                  @let barH = barHeight(item.maliyetUsd);
                  @let x = 15 + idx * 58;
                  @let y = 80 - barH;
                  <g>
                    <rect
                      [attr.x]="x"
                      [attr.y]="y"
                      width="34"
                      [attr.height]="barH"
                      rx="6"
                      class="fill-brand-500/80 hover:fill-brand-400 transition"
                    />
                    <text
                      [attr.x]="x + 17"
                      [attr.y]="y - 5"
                      text-anchor="middle"
                      class="fill-slate-400 text-[9px] font-semibold"
                    >
                      \${{ item.maliyetUsd.toFixed(2) }}
                    </text>
                    <text
                      [attr.x]="x + 17"
                      y="98"
                      text-anchor="middle"
                      class="fill-slate-500 text-[9px]"
                    >
                      {{ item.ay }}
                    </text>
                  </g>
                }
              </svg>
            </div>
          </div>
        }
      } @else {
        <p class="text-xs text-slate-400">Kullanım verileri yükleniyor...</p>
      }
    </section>
  `
})
export class UsagePanelComponent implements OnInit {
  private readonly usageService = inject(UsageService);

  readonly usage = signal<MonthlyUsageDto | null>(null);
  protected readonly Math = Math;

  readonly budgetPercent = computed(() => {
    const u = this.usage();
    if (!u || u.aylikLimitUsd <= 0) return 0;
    return Math.round((u.tahminiMaliyetUsd / u.aylikLimitUsd) * 100);
  });

  readonly maxHistoryCost = computed(() => {
    const u = this.usage();
    if (!u || u.sonAltiAyGecmisi.length === 0) return 1;
    const maxVal = Math.max(...u.sonAltiAyGecmisi.map((x) => x.maliyetUsd));
    return maxVal > 0 ? maxVal : 1;
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.usageService.getMonthlyUsage().subscribe({
      next: (res) => this.usage.set(res),
      error: () => this.usage.set(null)
    });
  }

  barHeight(costUsd: number): number {
    const max = this.maxHistoryCost();
    const ratio = Math.min(1, Math.max(0, costUsd / max));
    return Math.max(4, Math.round(ratio * 65));
  }
}

import { CurrencyPipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  inject,
  signal
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { T } from '../../core/i18n/tr';
import { SentezIsDurumu } from '../../core/models/enums';
import { SynthesisJobDto } from '../../core/models/synthesis.model';
import { SignalrService } from '../../core/services/signalr.service';
import { SynthesisService } from '../../core/services/synthesis.service';
import { ToastService } from '../../core/services/toast.service';
import { ConfirmService } from '../../shared/components/confirm-dialog.component';
import { EmptyStateComponent } from '../../shared/components/empty-state.component';
import { ProgressBarComponent } from '../../shared/components/progress-bar.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge.component';
import { DurationPipe } from '../../shared/pipes/duration.pipe';
import { RelativeTimePipe } from '../../shared/pipes/relative-time.pipe';

@Component({
  selector: 'app-jobs-page',
  standalone: true,
  imports: [
    FormsModule,
    RouterLink,
    CurrencyPipe,
    StatusBadgeComponent,
    ProgressBarComponent,
    EmptyStateComponent,
    DurationPipe,
    RelativeTimePipe
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="max-w-6xl mx-auto px-4 md:px-6 py-6 space-y-6">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <h1 class="text-2xl font-bold text-slate-900 dark:text-slate-100">
          {{ t.jobs.title }}
        </h1>

        <select
          [ngModel]="statusFilter()"
          (ngModelChange)="onFilterChange($event)"
          aria-label="İş durumu filtresi"
          class="px-3.5 py-2.5 min-h-[44px] rounded-xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-800 text-sm"
        >
          <option value="">{{ t.library.allStatuses }}</option>
          <option value="Calisiyor">{{ t.jobStatus.Calisiyor }}</option>
          <option value="Kuyrukta">{{ t.jobStatus.Kuyrukta }}</option>
          <option value="Tamamlandi">{{ t.jobStatus.Tamamlandi }}</option>
          <option value="KismiTamamlandi">{{ t.jobStatus.KismiTamamlandi }}</option>
          <option value="Hata">{{ t.jobStatus.Hata }}</option>
          <option value="IptalEdildi">{{ t.jobStatus.IptalEdildi }}</option>
        </select>
      </div>

      @if (jobs().length === 0 && !loading()) {
        <app-empty-state [title]="t.jobs.empty" />
      } @else {
        <div class="space-y-3">
          @for (job of jobs(); track job.id) {
            <div
              class="rounded-2xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-800 p-5 space-y-3"
            >
              <div class="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <a
                    [routerLink]="['/kitap', job.bookId]"
                    class="text-base font-bold text-slate-900 dark:text-slate-100 hover:text-brand-400"
                  >
                    {{ job.kitapBasligi }}
                  </a>
                  <p class="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
                    Ses Profili: <span class="font-medium">{{ job.sesProfiliAdi }}</span> ·
                    {{ job.olusturmaTarihi | relativeTime }}
                    @if (job.kullanilanMotorlar) {
                      <span> · Motor: {{ job.kullanilanMotorlar }}</span>
                    }
                  </p>
                </div>

                <app-status-badge [status]="job.durum" kind="job" />
              </div>

              <div class="space-y-1">
                <div class="flex justify-between text-xs text-slate-400">
                  <span>{{ job.mesaj || '—' }}</span>
                  <span>
                    {{ job.tamamlananBolumSayisi }} / {{ job.toplamBolumSayisi }} bölüm (%{{
                      job.yuzde
                    }})
                  </span>
                </div>
                <app-progress-bar [value]="job.yuzde" />
              </div>

              <div class="flex flex-wrap items-center justify-between gap-3 pt-1 text-xs text-slate-400">
                <div class="flex flex-wrap gap-4">
                  <span>Ses: {{ (job.gercekSesSureSn || job.tahminiSesSureSn) | duration }}</span>
                  <span>GPU: {{ (job.gercekGpuSureSn || job.tahminiGpuSureSn) | duration }}</span>
                  <span>
                    Maliyet: {{ job.tahminiMaliyetUsd | currency: 'USD' : 'symbol' : '1.2-4' }}
                  </span>
                </div>

                <div class="flex items-center gap-2">
                  @if (job.durum === 'Kuyrukta' || job.durum === 'Calisiyor') {
                    <button
                      type="button"
                      (click)="cancelJob(job.id)"
                      class="px-3 py-1.5 min-h-[36px] rounded-xl bg-rose-600/20 text-rose-400 hover:bg-rose-600 hover:text-white font-semibold transition"
                    >
                      {{ t.synthesis.cancelButton }}
                    </button>
                  }
                  @if (
                    job.durum === 'Hata' ||
                    job.durum === 'KismiTamamlandi' ||
                    job.durum === 'IptalEdildi'
                  ) {
                    <button
                      type="button"
                      (click)="retryJob(job.id)"
                      class="px-3 py-1.5 min-h-[36px] rounded-xl bg-brand-500 text-slate-950 font-semibold transition"
                    >
                      {{ t.synthesis.retryButton }}
                    </button>
                  }
                </div>
              </div>
            </div>
          }
        </div>
      }
    </div>
  `
})
export class JobsPageComponent implements OnInit {
  private readonly synthesisService = inject(SynthesisService);
  private readonly signalr = inject(SignalrService);
  private readonly confirm = inject(ConfirmService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly t = T;
  readonly jobs = signal<SynthesisJobDto[]>([]);
  readonly loading = signal(true);
  readonly statusFilter = signal<SentezIsDurumu | ''>('');

  ngOnInit(): void {
    this.loadJobs();

    this.signalr.synthesisProgress$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.loadJobs());

    this.signalr.synthesisFinished$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.loadJobs());
  }

  loadJobs(): void {
    this.loading.set(true);
    this.synthesisService
      .getList({ durum: this.statusFilter(), sayfa: 1, boyut: 30 })
      .subscribe({
        next: (res) => {
          this.jobs.set(res.kayitlar);
          this.loading.set(false);
        },
        error: () => this.loading.set(false)
      });
  }

  onFilterChange(status: SentezIsDurumu | ''): void {
    this.statusFilter.set(status);
    this.loadJobs();
  }

  async cancelJob(id: string): Promise<void> {
    const ok = await this.confirm.ask({
      title: 'İşi İptal Et',
      message: 'Bu seslendirme işini iptal etmek istediğinize emin misiniz?',
      danger: true
    });
    if (!ok) return;

    this.synthesisService.cancel(id).subscribe(() => {
      this.toast.info('İş iptal edildi.');
      this.loadJobs();
    });
  }

  retryJob(id: string): void {
    this.synthesisService.retry(id).subscribe(() => {
      this.toast.success('Eksik bölümler için yeniden seslendirme başlatıldı.');
      this.loadJobs();
    });
  }
}

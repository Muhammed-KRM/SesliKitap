import { CurrencyPipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  effect,
  inject,
  input,
  output,
  signal
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Subject, debounceTime } from 'rxjs';
import { T } from '../../core/i18n/tr';
import { SynthesisEstimateDto, SynthesisJobDto } from '../../core/models/synthesis.model';
import { VoiceProfileDto } from '../../core/models/voice-profile.model';
import { MediaTokenService } from '../../core/services/media-token.service';
import { SignalrService } from '../../core/services/signalr.service';
import { SynthesisService } from '../../core/services/synthesis.service';
import { ToastService } from '../../core/services/toast.service';
import { VoiceProfileService } from '../../core/services/voice-profile.service';
import { ConfirmService } from '../../shared/components/confirm-dialog.component';
import { ProgressBarComponent } from '../../shared/components/progress-bar.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge.component';
import { DurationPipe } from '../../shared/pipes/duration.pipe';

export type SynthesisScope = 'missing' | 'all' | 'selected';

@Component({
  selector: 'app-synthesis-panel',
  standalone: true,
  imports: [
    FormsModule,
    CurrencyPipe,
    ProgressBarComponent,
    StatusBadgeComponent,
    DurationPipe
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
      <!-- Sol: Ses Profili ve Kapsam Seçimi -->
      <div
        class="rounded-2xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-800 p-6 space-y-5"
      >
        <h3 class="text-base font-bold text-slate-900 dark:text-slate-100">
          {{ t.synthesis.title }}
        </h3>

        <div>
          <label class="block text-xs font-semibold uppercase tracking-wider text-slate-400 mb-2">
            {{ t.synthesis.selectVoice }}
          </label>
          <div class="space-y-2">
            @for (vp of readyVoices(); track vp.id) {
              <div
                (click)="onVoiceChange(vp.id)"
                class="p-3 rounded-xl border cursor-pointer flex items-center justify-between gap-3 transition"
                [class]="
                  selectedVoiceId() === vp.id
                    ? 'border-brand-500 bg-brand-500/10'
                    : 'border-slate-200 dark:border-slate-700 hover:border-slate-500'
                "
              >
                <div>
                  <div class="flex items-center gap-2">
                    <span class="text-sm font-semibold text-slate-900 dark:text-slate-100">
                      {{ vp.ad }}
                    </span>
                    @if (vp.varsayilanMi) {
                      <span
                        class="px-2 py-0.5 rounded-full text-[10px] font-semibold bg-brand-500/20 text-brand-400"
                      >
                        {{ t.voices.defaultBadge }}
                      </span>
                    }
                  </div>
                  @if (vp.aciklama) {
                    <p class="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
                      {{ vp.aciklama }}
                    </p>
                  }
                </div>

                @if (vp.ornekSesVar || vp.ornekVar) {
                  <button
                    type="button"
                    (click)="playVoiceSample($event, vp.id)"
                    class="px-3 py-1.5 rounded-lg bg-slate-200 dark:bg-slate-700 hover:bg-brand-500 hover:text-slate-950 text-xs font-semibold transition"
                  >
                    ▶ {{ t.voices.listenSample }}
                  </button>
                }
              </div>
            }
          </div>
        </div>

        <div>
          <label class="block text-xs font-semibold uppercase tracking-wider text-slate-400 mb-2">
            Seslendirme Kapsamı
          </label>
          <div class="space-y-2 text-sm">
            <label class="flex items-center gap-2.5 cursor-pointer">
              <input
                type="radio"
                name="scope"
                value="missing"
                [ngModel]="scope()"
                (ngModelChange)="onScopeChange($event)"
                class="text-brand-500 focus:ring-brand-500"
              />
              <span>{{ t.synthesis.scopeMissing }}</span>
            </label>
            <label class="flex items-center gap-2.5 cursor-pointer">
              <input
                type="radio"
                name="scope"
                value="all"
                [ngModel]="scope()"
                (ngModelChange)="onScopeChange($event)"
                class="text-brand-500 focus:ring-brand-500"
              />
              <span>{{ t.synthesis.scopeAll }}</span>
            </label>
            @if (preselectedChapterIds().length > 0) {
              <label class="flex items-center gap-2.5 cursor-pointer">
                <input
                  type="radio"
                  name="scope"
                  value="selected"
                  [ngModel]="scope()"
                  (ngModelChange)="onScopeChange($event)"
                  class="text-brand-500 focus:ring-brand-500"
                />
                <span>
                  {{ t.synthesis.scopeSelected }} ({{ preselectedChapterIds().length }} bölüm)
                </span>
              </label>
            }
          </div>
        </div>
      </div>

      <!-- Sağ: Maliyet Tahmini veya Canlı Sentez İlerlemesi -->
      <div
        class="rounded-2xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-800 p-6 flex flex-col justify-between space-y-5"
      >
        @if (activeJob(); as job) {
          <div class="space-y-4">
            <div class="flex items-center justify-between">
              <h3 class="text-base font-bold text-slate-900 dark:text-slate-100">
                Canlı Seslendirme İlerlemesi
              </h3>
              <app-status-badge [status]="liveProgress() ? 'Calisiyor' : job.durum" kind="job" />
            </div>

            <div class="space-y-1.5">
              <div class="flex justify-between text-xs text-slate-400">
                <span>{{ liveProgress()?.mesaj || job.mesaj || 'Seslendiriliyor...' }}</span>
                <span class="font-bold text-brand-400">
                  %{{ liveProgress()?.genelYuzde ?? job.yuzde }}
                </span>
              </div>
              <app-progress-bar [value]="liveProgress()?.genelYuzde ?? job.yuzde" />
            </div>

            @if (liveProgress(); as lp) {
              <div class="space-y-1">
                <div class="flex justify-between text-[11px] text-slate-500">
                  <span>Mevcut Bölüm İlerlemesi</span>
                  <span>%{{ lp.bolumYuzde }}</span>
                </div>
                <app-progress-bar [value]="lp.bolumYuzde" variant="emerald" />
              </div>
            }

            <div class="grid grid-cols-2 gap-3 text-xs pt-2">
              <div class="p-3 rounded-xl bg-slate-50 dark:bg-surface-900">
                <span class="text-slate-400 block">Tamamlanan Bölüm</span>
                <span class="text-sm font-bold text-slate-900 dark:text-slate-100">
                  {{ job.tamamlananBolum }} / {{ job.toplamBolum }}
                </span>
              </div>
              <div class="p-3 rounded-xl bg-slate-50 dark:bg-surface-900">
                <span class="text-slate-400 block">Tahmini Maliyet</span>
                <span class="text-sm font-bold text-slate-900 dark:text-slate-100">
                  {{ job.tahminiMaliyetUsd | currency: 'USD' : 'symbol' : '1.2-4' }}
                </span>
              </div>
            </div>
          </div>

          <div class="flex items-center justify-end gap-3 pt-4">
            @if (job.durum === 'Kuyrukta' || job.durum === 'Calisiyor') {
              <button
                type="button"
                (click)="cancelActiveJob(job.id)"
                class="px-4 py-2.5 min-h-[44px] rounded-xl bg-rose-600 hover:bg-rose-500 text-white font-semibold text-sm transition"
              >
                {{ t.synthesis.cancelButton }}
              </button>
            }
          </div>
        } @else {
          @if (estimate(); as est) {
          <div class="space-y-4">
            <h3 class="text-base font-bold text-slate-900 dark:text-slate-100">
              {{ t.synthesis.estimateTitle }}
            </h3>

            <div class="grid grid-cols-2 gap-3">
              <div class="p-3.5 rounded-xl bg-slate-50 dark:bg-surface-900">
                <span class="text-xs text-slate-400 block">Seçili Bölüm / Kelime</span>
                <span class="text-sm font-bold text-slate-900 dark:text-slate-100">
                  {{ est.bolumSayisi }} bölüm · {{ est.kelimeSayisi }} kelime
                </span>
              </div>
              <div class="p-3.5 rounded-xl bg-slate-50 dark:bg-surface-900">
                <span class="text-xs text-slate-400 block">{{ t.synthesis.estimatedAudio }}</span>
                <span class="text-sm font-bold text-slate-900 dark:text-slate-100">
                  {{ est.tahminiSesSn | duration }}
                </span>
              </div>
              <div class="p-3.5 rounded-xl bg-slate-50 dark:bg-surface-900">
                <span class="text-xs text-slate-400 block">{{ t.synthesis.estimatedGpu }}</span>
                <span class="text-sm font-bold text-slate-900 dark:text-slate-100">
                  {{ est.tahminiGpuSn | duration }}
                </span>
              </div>
              <div class="p-3.5 rounded-xl bg-slate-50 dark:bg-surface-900">
                <span class="text-xs text-slate-400 block">{{ t.synthesis.estimatedCost }}</span>
                <span class="text-sm font-bold text-brand-400">
                  {{ est.tahminiMaliyetUsd | currency: 'USD' : 'symbol' : '1.2-4' }}
                </span>
              </div>
            </div>

            <div class="space-y-1.5">
              <div class="flex justify-between text-xs text-slate-400">
                <span>{{ t.synthesis.monthlyBudget }}</span>
                <span>
                  {{ est.buAyHarcananUsd | currency: 'USD' }} /
                  {{ est.aylikButceUsd | currency: 'USD' }}
                </span>
              </div>
              <app-progress-bar
                [value]="budgetPercent()"
                [variant]="est.butceAsiliyor ? 'rose' : 'emerald'"
              />
            </div>

            @if (est.butceAsiliyor) {
              <div
                class="p-3 rounded-xl bg-amber-500/15 border border-amber-500/30 text-amber-300 text-xs space-y-2"
              >
                <p>{{ t.synthesis.budgetExceeded }}</p>
                <label class="flex items-center gap-2 font-semibold cursor-pointer">
                  <input
                    type="checkbox"
                    [(ngModel)]="forceApproved"
                    class="rounded text-brand-500"
                  />
                  <span>{{ t.synthesis.forceApprove }}</span>
                </label>
              </div>
            }
          </div>

          <button
            type="button"
            (click)="startSynthesis()"
            [disabled]="
              starting() ||
              est.bolumSayisi === 0 ||
              (est.butceAsiliyor && !forceApproved)
            "
            class="w-full min-h-[44px] py-3 px-5 rounded-xl bg-brand-500 hover:bg-brand-600 disabled:opacity-50 text-slate-950 font-semibold text-sm transition"
          >
            {{ t.synthesis.startButton }}
          </button>
          } @else {
          <div class="text-sm text-slate-400 py-8 text-center">
            Seslendirme tahmini için hazır bir ses profili seçin.
          </div>
          }
        }
      </div>
    </div>
  `
})
export class SynthesisPanelComponent implements OnInit {
  private readonly synthesisService = inject(SynthesisService);
  private readonly voiceService = inject(VoiceProfileService);
  private readonly mediaToken = inject(MediaTokenService);
  private readonly signalr = inject(SignalrService);
  private readonly confirm = inject(ConfirmService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly bookId = input.required<string>();
  readonly defaultVoiceId = input<string | null>(null);
  readonly preselectedChapterIds = input<string[]>([]);
  readonly jobStarted = output<SynthesisJobDto>();

  readonly t = T;
  readonly voices = signal<VoiceProfileDto[]>([]);
  readonly selectedVoiceId = signal<string | null>(null);
  readonly scope = signal<SynthesisScope>('missing');
  readonly estimate = signal<SynthesisEstimateDto | null>(null);
  readonly activeJob = signal<SynthesisJobDto | null>(null);
  readonly starting = signal(false);
  forceApproved = false;

  readonly readyVoices = computed(() => this.voices().filter((v) => v.durum === 'Hazir'));

  readonly liveProgress = computed(() => this.signalr.synthesisProgress()[this.bookId()] ?? null);

  readonly budgetPercent = computed(() => {
    const est = this.estimate();
    if (!est || est.aylikButceUsd <= 0) return 0;
    return Math.min(
      100,
      Math.round(((est.buAyHarcananUsd + est.tahminiMaliyetUsd) / est.aylikButceUsd) * 100)
    );
  });

  private readonly estimateTrigger$ = new Subject<void>();
  private sampleAudio: HTMLAudioElement | null = null;

  constructor() {
    effect(
      () => {
        const ids = this.preselectedChapterIds();
        if (ids.length > 0) {
          this.scope.set('selected');
          this.triggerEstimate();
        }
      },
      { allowSignalWrites: true }
    );
  }

  ngOnInit(): void {
    this.estimateTrigger$
      .pipe(debounceTime(400), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.fetchEstimate());

    this.loadVoices();
    this.checkActiveJob();

    this.signalr.chapterReady$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((msg) => {
        if (msg.bookId === this.bookId()) {
          this.checkActiveJob();
        }
      });

    this.signalr.synthesisFinished$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((msg) => {
        if (msg.bookId === this.bookId()) {
          this.activeJob.set(null);
          this.fetchEstimate();
        }
      });
  }

  loadVoices(): void {
    this.voiceService.getList().subscribe((list) => {
      this.voices.set(list);
      const ready = list.filter((v) => v.durum === 'Hazir');
      const def =
        ready.find((v) => v.id === this.defaultVoiceId()) ??
        ready.find((v) => v.varsayilanMi) ??
        ready[0];
      if (def) {
        this.selectedVoiceId.set(def.id);
        this.triggerEstimate();
      }
    });
  }

  checkActiveJob(): void {
    this.synthesisService.getList({ sayfa: 1, boyut: 10 }).subscribe((res) => {
      const running = res.kayitlar.find(
        (j) =>
          j.bookId === this.bookId() && (j.durum === 'Kuyrukta' || j.durum === 'Calisiyor')
      );
      if (running) {
        this.activeJob.set(running);
      }
    });
  }

  onVoiceChange(voiceId: string): void {
    this.selectedVoiceId.set(voiceId);
    this.triggerEstimate();
  }

  onScopeChange(nextScope: SynthesisScope): void {
    this.scope.set(nextScope);
    this.triggerEstimate();
  }

  triggerEstimate(): void {
    this.estimateTrigger$.next();
  }

  private fetchEstimate(): void {
    const voiceId = this.selectedVoiceId();
    if (!voiceId) return;

    const currentScope = this.scope();
    const chapterIds =
      currentScope === 'selected' && this.preselectedChapterIds().length > 0
        ? this.preselectedChapterIds()
        : undefined;
    const sadeceEksikler = currentScope === 'missing';

    this.synthesisService
      .estimate(this.bookId(), voiceId, { chapterIds, sadeceEksikler })
      .subscribe((est) => this.estimate.set(est));
  }

  startSynthesis(): void {
    const voiceId = this.selectedVoiceId();
    if (!voiceId || this.starting()) return;

    const currentScope = this.scope();
    const chapterIds =
      currentScope === 'selected' && this.preselectedChapterIds().length > 0
        ? this.preselectedChapterIds()
        : null;
    const sadeceEksikler = currentScope === 'missing';

    this.starting.set(true);
    this.synthesisService
      .start(this.bookId(), {
        voiceProfileId: voiceId,
        chapterIds,
        sadeceEksikler,
        force: this.forceApproved
      })
      .subscribe({
        next: (job) => {
          this.starting.set(false);
          this.activeJob.set(job);
          this.toast.success('Seslendirme işi başlatıldı.');
          this.jobStarted.emit(job);
        },
        error: () => {
          this.starting.set(false);
        }
      });
  }

  async cancelActiveJob(jobId: string): Promise<void> {
    const ok = await this.confirm.ask({
      title: 'Seslendirmeyi İptal Et',
      message: 'Devam eden seslendirme işini durdurmak istediğinize emin misiniz?',
      danger: true
    });
    if (!ok) return;

    this.synthesisService.cancel(jobId).subscribe(() => {
      this.toast.info('İptal isteği gönderildi.');
      this.activeJob.set(null);
    });
  }

  async playVoiceSample(event: Event, voiceId: string): Promise<void> {
    event.stopPropagation();
    if (this.sampleAudio) {
      this.sampleAudio.pause();
      this.sampleAudio = null;
    }
    const url = await this.mediaToken.sampleUrl(voiceId);
    this.sampleAudio = new Audio(url);
    void this.sampleAudio.play();
  }
}

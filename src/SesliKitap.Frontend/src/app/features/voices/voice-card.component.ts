import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { T } from '../../core/i18n/tr';
import { VoiceProfileDto } from '../../core/models/voice-profile.model';
import { MediaTokenService } from '../../core/services/media-token.service';
import { StatusBadgeComponent } from '../../shared/components/status-badge.component';

@Component({
  selector: 'app-voice-card',
  standalone: true,
  imports: [StatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="rounded-2xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-800 p-5 flex flex-col justify-between gap-4 shadow-xs"
    >
      <div class="space-y-2">
        <div class="flex items-start justify-between gap-2">
          <div>
            <div class="flex items-center gap-2">
              <h3 class="text-base font-bold text-slate-900 dark:text-slate-100">
                {{ profile().ad }}
              </h3>
              @if (profile().varsayilanMi) {
                <span
                  class="px-2 py-0.5 rounded-full text-[10px] font-semibold bg-brand-500/20 text-brand-400"
                >
                  {{ t.voices.defaultBadge }}
                </span>
              }
            </div>
            <p class="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
              {{ profile().cinsiyet || 'Belirtilmemiş' }} · Referans: {{ (profile().referansSureSn ?? 0).toFixed(1) }} sn
            </p>
          </div>
          <app-status-badge [status]="profile().durum" kind="voice" />
        </div>

        @if (profile().aciklama) {
          <p class="text-xs text-slate-600 dark:text-slate-300">{{ profile().aciklama }}</p>
        }

        @if (profile().hataMesaji) {
          <p class="text-xs text-rose-400">{{ profile().hataMesaji }}</p>
        }
      </div>

      @if (sampleUrl()) {
        <audio [src]="sampleUrl()!" controls class="w-full h-9"></audio>
      }

      <div class="flex flex-wrap items-center justify-between gap-2 pt-2 border-t border-slate-100 dark:border-slate-800">
        <div class="flex items-center gap-2">
          @if ((profile().ornekSesVar || profile().ornekVar) && !sampleUrl()) {
            <button
              type="button"
              (click)="loadSample()"
              class="px-3 py-1.5 min-h-[36px] rounded-xl bg-brand-500/15 hover:bg-brand-500/25 text-brand-400 text-xs font-semibold transition"
            >
              ▶ {{ t.voices.listenSample }}
            </button>
          }
          @if (!profile().varsayilanMi && profile().durum === 'Hazir') {
            <button
              type="button"
              (click)="setDefault.emit(profile().id)"
              class="px-3 py-1.5 min-h-[36px] rounded-xl bg-slate-100 dark:bg-slate-700 hover:bg-slate-200 text-xs font-medium transition"
            >
              {{ t.voices.makeDefault }}
            </button>
          }
        </div>

        <button
          type="button"
          (click)="deleteProfile.emit(profile().id)"
          class="px-2.5 py-1.5 min-h-[36px] rounded-xl text-rose-400 hover:bg-rose-500/10 text-xs font-medium transition"
        >
          {{ t.common.delete }}
        </button>
      </div>
    </div>
  `
})
export class VoiceCardComponent {
  private readonly mediaToken = inject(MediaTokenService);

  readonly profile = input.required<VoiceProfileDto>();
  readonly setDefault = output<string>();
  readonly deleteProfile = output<string>();

  readonly t = T;
  readonly sampleUrl = signal<string | null>(null);

  async loadSample(): Promise<void> {
    const url = await this.mediaToken.sampleUrl(this.profile().id);
    this.sampleUrl.set(url);
  }
}

import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { T } from '../../core/i18n/tr';
import {
  BolumSesDurumu,
  KitapDurumu,
  SentezIsDurumu,
  SesProfiliDurumu
} from '../../core/models/enums';

export type BadgeStatus =
  | KitapDurumu
  | BolumSesDurumu
  | SentezIsDurumu
  | SesProfiliDurumu;

@Component({
  selector: 'app-status-badge',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span
      class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-medium border"
      [class]="badgeClasses()"
    >
      <span class="w-1.5 h-1.5 rounded-full" [class]="dotClasses()"></span>
      {{ label() }}
    </span>
  `
})
export class StatusBadgeComponent {
  readonly status = input.required<BadgeStatus>();
  readonly kind = input<'book' | 'chapter' | 'job' | 'voice'>('book');

  readonly label = computed(() => {
    const s = this.status();
    const k = this.kind();
    if (k === 'chapter' && s in T.chapterAudioStatus) {
      return T.chapterAudioStatus[s as BolumSesDurumu];
    }
    if (k === 'job' && s in T.jobStatus) {
      return T.jobStatus[s as SentezIsDurumu];
    }
    if (k === 'voice' && s in T.voiceStatus) {
      return T.voiceStatus[s as SesProfiliDurumu];
    }
    if (s in T.status) {
      return T.status[s as KitapDurumu];
    }
    return s;
  });

  readonly badgeClasses = computed(() => {
    const s = this.status();
    switch (s) {
      case 'Hazir':
      case 'Tamamlandi':
        return 'bg-emerald-500/15 text-emerald-400 border-emerald-500/30';
      case 'Isleniyor':
      case 'Indiriliyor':
      case 'Seslendiriliyor':
      case 'Calisiyor':
      case 'Kuyrukta':
        return 'bg-brand-500/15 text-brand-400 border-brand-500/30';
      case 'OnizlemeBekliyor':
      case 'KismenHazir':
      case 'KismiTamamlandi':
        return 'bg-sky-500/15 text-sky-400 border-sky-500/30';
      case 'GuncelDegil':
      case 'OcrGerekli':
      case 'IptalEdildi':
        return 'bg-amber-500/15 text-amber-400 border-amber-500/30';
      case 'Hata':
        return 'bg-rose-500/15 text-rose-400 border-rose-500/30';
      default:
        return 'bg-slate-500/15 text-slate-400 border-slate-500/30';
    }
  });

  readonly dotClasses = computed(() => {
    const s = this.status();
    switch (s) {
      case 'Hazir':
      case 'Tamamlandi':
        return 'bg-emerald-400';
      case 'Isleniyor':
      case 'Indiriliyor':
      case 'Seslendiriliyor':
      case 'Calisiyor':
        return 'bg-brand-400 animate-pulse';
      case 'OnizlemeBekliyor':
      case 'KismenHazir':
      case 'KismiTamamlandi':
        return 'bg-sky-400';
      case 'GuncelDegil':
      case 'OcrGerekli':
        return 'bg-amber-400';
      case 'Hata':
        return 'bg-rose-400';
      default:
        return 'bg-slate-400';
    }
  });
}

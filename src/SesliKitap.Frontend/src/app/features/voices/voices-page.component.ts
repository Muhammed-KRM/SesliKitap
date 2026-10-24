import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  inject,
  signal
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { T } from '../../core/i18n/tr';
import { VoiceProfileDto } from '../../core/models/voice-profile.model';
import { SignalrService } from '../../core/services/signalr.service';
import { ToastService } from '../../core/services/toast.service';
import { VoiceProfileService } from '../../core/services/voice-profile.service';
import { ConfirmService } from '../../shared/components/confirm-dialog.component';
import { EmptyStateComponent } from '../../shared/components/empty-state.component';
import { VoiceCardComponent } from './voice-card.component';
import { VoiceFormComponent } from './voice-form.component';

@Component({
  selector: 'app-voices-page',
  standalone: true,
  imports: [VoiceCardComponent, VoiceFormComponent, EmptyStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="max-w-6xl mx-auto px-4 md:px-6 py-6 space-y-6">
      <div class="flex items-center justify-between gap-3">
        <h1 class="text-2xl font-bold text-slate-900 dark:text-slate-100">
          {{ t.voices.title }}
        </h1>
        @if (!showForm()) {
          <button
            type="button"
            (click)="showForm.set(true)"
            class="min-h-[44px] px-4 py-2.5 rounded-xl bg-brand-500 hover:bg-brand-600 text-slate-950 font-semibold text-sm transition"
          >
            + {{ t.voices.createNew }}
          </button>
        }
      </div>

      @if (showForm()) {
        <app-voice-form
          (created)="onCreated($event)"
          (cancelled)="showForm.set(false)"
        />
      }

      @if (profiles().length === 0 && !loading()) {
        <app-empty-state
          title="Henüz ses profili yok"
          description="10–20 saniyelik temiz bir ses kaydı yükleyerek veya mikrofonla kaydederek ilk ses profilinizi oluşturun."
          [actionLabel]="t.voices.createNew"
          (actionClick)="showForm.set(true)"
        />
      } @else {
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          @for (vp of profiles(); track vp.id) {
            <app-voice-card
              [profile]="vp"
              (setDefault)="onSetDefault($event)"
              (deleteProfile)="onDelete($event)"
            />
          }
        </div>
      }
    </div>
  `
})
export class VoicesPageComponent implements OnInit {
  private readonly voiceService = inject(VoiceProfileService);
  private readonly signalr = inject(SignalrService);
  private readonly confirm = inject(ConfirmService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly t = T;
  readonly profiles = signal<VoiceProfileDto[]>([]);
  readonly loading = signal(true);
  readonly showForm = signal(false);

  ngOnInit(): void {
    this.loadProfiles();

    this.signalr.voiceProfileReady$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.loadProfiles();
      });
  }

  loadProfiles(): void {
    this.loading.set(true);
    this.voiceService.getList().subscribe({
      next: (list) => {
        this.profiles.set(list);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  onCreated(vp: VoiceProfileDto): void {
    this.showForm.set(false);
    this.profiles.update((prev) => [vp, ...prev]);
  }

  onSetDefault(id: string): void {
    this.voiceService.setDefault(id).subscribe(() => {
      this.toast.success('Varsayılan ses profili güncellendi.');
      this.loadProfiles();
    });
  }

  async onDelete(id: string): Promise<void> {
    const ok = await this.confirm.ask({
      title: 'Ses Profilini Sil',
      message: 'Bu ses profilini silmek istediğinize emin misiniz?',
      danger: true
    });
    if (!ok) return;

    this.voiceService.delete(id).subscribe(() => {
      this.toast.success('Ses profili silindi.');
      this.loadProfiles();
    });
  }
}

import { ChangeDetectionStrategy, Component, inject, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { T } from '../../core/i18n/tr';
import { SesCinsiyeti } from '../../core/models/enums';
import { VoiceProfileDto } from '../../core/models/voice-profile.model';
import { ToastService } from '../../core/services/toast.service';
import { VoiceProfileService } from '../../core/services/voice-profile.service';
import { VoiceRecorderComponent } from './voice-recorder.component';

@Component({
  selector: 'app-voice-form',
  standalone: true,
  imports: [FormsModule, VoiceRecorderComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="rounded-2xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-800 p-6 space-y-4"
    >
      <div class="flex items-center justify-between">
        <h2 class="text-base font-bold text-slate-900 dark:text-slate-100">
          {{ t.voices.createNew }}
        </h2>
        <button
          type="button"
          (click)="cancelled.emit()"
          class="text-xs text-slate-400 hover:text-slate-200"
        >
          {{ t.common.close }}
        </button>
      </div>

      <p class="text-xs text-slate-500 dark:text-slate-400">
        {{ t.voices.tips }}
      </p>

      <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div>
          <label class="block text-xs font-medium text-slate-500 dark:text-slate-400 mb-1">
            {{ t.voices.nameLabel }} *
          </label>
          <input
            type="text"
            [(ngModel)]="name"
            class="w-full px-3.5 py-2.5 rounded-xl bg-slate-50 dark:bg-surface-900 border border-slate-300 dark:border-slate-700 text-sm"
          />
        </div>
        <div>
          <label class="block text-xs font-medium text-slate-500 dark:text-slate-400 mb-1">
            {{ t.voices.genderLabel }}
          </label>
          <select
            [(ngModel)]="gender"
            class="w-full px-3.5 py-2.5 rounded-xl bg-slate-50 dark:bg-surface-900 border border-slate-300 dark:border-slate-700 text-sm"
          >
            <option value="Belirtilmemis">Belirtilmemiş</option>
            <option value="Erkek">Erkek</option>
            <option value="Kadin">Kadın</option>
          </select>
        </div>
      </div>

      <div>
        <label class="block text-xs font-medium text-slate-500 dark:text-slate-400 mb-1">
          {{ t.voices.descriptionLabel }}
        </label>
        <input
          type="text"
          [(ngModel)]="description"
          class="w-full px-3.5 py-2.5 rounded-xl bg-slate-50 dark:bg-surface-900 border border-slate-300 dark:border-slate-700 text-sm"
        />
      </div>

      <!-- Kaynak Seçimi: Dosya veya Mikrofon -->
      <div class="flex items-center gap-2 border-b border-slate-200 dark:border-slate-800 pb-2">
        <button
          type="button"
          (click)="sourceTab.set('upload')"
          class="px-3 py-1.5 rounded-lg text-xs font-semibold transition"
          [class]="
            sourceTab() === 'upload'
              ? 'bg-brand-500 text-slate-950'
              : 'bg-slate-100 dark:bg-surface-900 text-slate-400'
          "
        >
          {{ t.voices.uploadTab }}
        </button>
        <button
          type="button"
          (click)="sourceTab.set('record')"
          class="px-3 py-1.5 rounded-lg text-xs font-semibold transition"
          [class]="
            sourceTab() === 'record'
              ? 'bg-brand-500 text-slate-950'
              : 'bg-slate-100 dark:bg-surface-900 text-slate-400'
          "
        >
          {{ t.voices.recordTab }}
        </button>
      </div>

      @if (sourceTab() === 'upload') {
        <div>
          <input
            type="file"
            accept=".wav,.mp3,.m4a,.ogg,.flac,.webm"
            (change)="onFileSelected($event)"
            class="block w-full text-xs text-slate-400 file:mr-3 file:py-2 file:px-4 file:rounded-xl file:border-0 file:text-xs file:font-semibold file:bg-brand-500/15 file:text-brand-400"
          />
        </div>
      } @else {
        <div class="space-y-2">
          <div class="p-3 rounded-xl bg-brand-500/10 border border-brand-500/30 text-xs">
            <p class="font-semibold text-brand-400 mb-1">Önerilen Okuma Metni:</p>
            <p class="reader-serif text-slate-700 dark:text-slate-200 leading-relaxed">
              "{{ t.voices.suggestedPrompt }}"
            </p>
            <button
              type="button"
              (click)="useSuggestedPrompt()"
              class="mt-2 text-xs font-semibold text-brand-400 underline"
            >
              {{ t.voices.useSuggestedPrompt }}
            </button>
          </div>
          <app-voice-recorder (recorded)="audioFile.set($event)" />
        </div>
      }

      <div>
        <div class="flex items-center justify-between mb-1">
          <label class="text-xs font-medium text-slate-500 dark:text-slate-400">
            {{ t.voices.promptTextLabel }}
          </label>
          <button
            type="button"
            (click)="useSuggestedPrompt()"
            class="text-[11px] font-semibold text-brand-400 hover:underline"
          >
            {{ t.voices.useSuggestedPrompt }}
          </button>
        </div>
        <textarea
          [(ngModel)]="promptText"
          rows="2"
          class="w-full p-3 rounded-xl bg-slate-50 dark:bg-surface-900 border border-slate-300 dark:border-slate-700 text-xs"
        ></textarea>
      </div>

      <label class="flex items-start gap-2.5 text-xs text-slate-600 dark:text-slate-300 cursor-pointer">
        <input
          type="checkbox"
          [(ngModel)]="consentGiven"
          class="mt-0.5 rounded text-brand-500 focus:ring-brand-500"
        />
        <span>{{ t.voices.consentLabel }}</span>
      </label>

      <div class="flex items-center justify-end gap-3 pt-2">
        <button
          type="button"
          (click)="cancelled.emit()"
          class="px-4 py-2.5 min-h-[44px] rounded-xl text-sm font-medium text-slate-500 hover:bg-slate-100 dark:hover:bg-slate-700/50"
        >
          {{ t.common.cancel }}
        </button>
        <button
          type="button"
          (click)="submit()"
          [disabled]="!canSubmit() || saving()"
          class="px-5 py-2.5 min-h-[44px] rounded-xl bg-brand-500 hover:bg-brand-600 disabled:opacity-50 text-slate-950 font-semibold text-sm transition"
        >
          {{ saving() ? t.common.loading : t.common.save }}
        </button>
      </div>
    </div>
  `
})
export class VoiceFormComponent {
  private readonly voiceService = inject(VoiceProfileService);
  private readonly toast = inject(ToastService);

  readonly created = output<VoiceProfileDto>();
  readonly cancelled = output<void>();

  readonly t = T;
  readonly sourceTab = signal<'upload' | 'record'>('upload');
  readonly audioFile = signal<File | null>(null);
  readonly saving = signal(false);

  name = '';
  description = '';
  gender: SesCinsiyeti = 'Belirtilmemis';
  promptText = '';
  consentGiven = false;

  onFileSelected(event: Event): void {
    const el = event.target as HTMLInputElement;
    if (el.files && el.files.length > 0) {
      this.audioFile.set(el.files[0]);
    }
  }

  useSuggestedPrompt(): void {
    this.promptText = T.voices.suggestedPrompt;
  }

  canSubmit(): boolean {
    return Boolean(this.name.trim() && this.audioFile() && this.consentGiven);
  }

  submit(): void {
    const file = this.audioFile();
    if (!file || !this.canSubmit() || this.saving()) return;

    this.saving.set(true);
    this.voiceService
      .create({
        ad: this.name,
        aciklama: this.description,
        cinsiyet: this.gender,
        referansMetin: this.promptText,
        onayVerildiMi: this.consentGiven,
        sesDosyasi: file
      })
      .subscribe({
        next: (vp) => {
          this.saving.set(false);
          this.toast.success('Ses profili oluşturuldu, referans ses hazırlanıyor...');
          this.created.emit(vp);
        },
        error: () => {
          this.saving.set(false);
        }
      });
  }
}

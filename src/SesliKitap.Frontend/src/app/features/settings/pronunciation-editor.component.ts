import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  PronunciationDto,
  PronunciationPreviewResponse
} from '../../core/models/faz8.model';
import { PronunciationService } from '../../core/services/pronunciation.service';
import { ToastService } from '../../core/services/toast.service';

@Component({
  selector: 'app-pronunciation-editor',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section
      class="rounded-2xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-800 p-6 space-y-5"
    >
      <div>
        <h2 class="text-base font-bold text-slate-900 dark:text-slate-100">
          Telaffuz Sözlüğü
        </h2>
        <p class="text-xs text-slate-500 dark:text-slate-400">
          Seslendirme öncesinde kısaltmaların, yabancı terimlerin ve özel isimlerin Türkçe okunuş
          kurallarını tanımlayın.
        </p>
      </div>

      <!-- Kural Ekle / Düzenle Formu -->
      <div class="p-4 rounded-xl bg-slate-50 dark:bg-surface-900 space-y-3">
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div>
            <label class="block text-xs font-medium text-slate-500 dark:text-slate-400 mb-1">
              Kaynak İfade (örn. AI, Prof., J.R.R.)
            </label>
            <input
              type="text"
              [(ngModel)]="kaynakIfade"
              placeholder="Kaynak kelime veya kısaltma"
              class="w-full px-3 py-2 rounded-xl bg-white dark:bg-surface-800 border border-slate-300 dark:border-slate-700 text-sm"
            />
          </div>
          <div>
            <label class="block text-xs font-medium text-slate-500 dark:text-slate-400 mb-1">
              Türkçe Okunuşu (örn. yapay zekâ, profesör, Cey Ar Ar)
            </label>
            <input
              type="text"
              [(ngModel)]="okunus"
              placeholder="Okunacak Türkçe karşılık"
              class="w-full px-3 py-2 rounded-xl bg-white dark:bg-surface-800 border border-slate-300 dark:border-slate-700 text-sm"
            />
          </div>
        </div>

        <div class="flex flex-wrap items-center justify-between gap-3 pt-1">
          <div class="flex flex-wrap items-center gap-4 text-xs">
            <label class="flex items-center gap-1.5 cursor-pointer">
              <input
                type="checkbox"
                [(ngModel)]="yalnizcaTamKelime"
                class="rounded accent-brand-500"
              />
              <span>Yalnızca tam kelime eşleşmesi</span>
            </label>
            <label class="flex items-center gap-1.5 cursor-pointer">
              <input
                type="checkbox"
                [(ngModel)]="buyukKucukHarfDuyarli"
                class="rounded accent-brand-500"
              />
              <span>Büyük/küçük harf duyarlı</span>
            </label>
          </div>

          <div class="flex items-center gap-2">
            @if (editingId()) {
              <button
                type="button"
                (click)="resetForm()"
                class="px-3 py-1.5 rounded-xl text-xs font-medium text-slate-400 hover:bg-slate-200 dark:hover:bg-slate-800"
              >
                Vazgeç
              </button>
            }
            <button
              type="button"
              (click)="saveRule()"
              class="px-4 py-2 min-h-[38px] rounded-xl bg-brand-500 hover:bg-brand-600 text-slate-950 text-xs font-semibold transition"
            >
              {{ editingId() ? 'Kuralı Güncelle' : '+ Kural Ekle' }}
            </button>
          </div>
        </div>
      </div>

      <!-- Kayıtlı Kurallar Tablosu -->
      @if (entries().length > 0) {
        <div class="overflow-x-auto rounded-xl border border-slate-200 dark:border-slate-800">
          <table class="w-full text-left text-xs">
            <thead class="bg-slate-50 dark:bg-surface-900 text-slate-400">
              <tr>
                <th class="px-3.5 py-2.5 font-semibold">Kaynak İfade</th>
                <th class="px-3.5 py-2.5 font-semibold">Okunuş</th>
                <th class="px-3.5 py-2.5 font-semibold">Eşleşme</th>
                <th class="px-3.5 py-2.5 font-semibold text-right">İşlem</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-200 dark:divide-slate-800">
              @for (item of entries(); track item.id) {
                <tr class="hover:bg-slate-50/60 dark:hover:bg-slate-800/40">
                  <td class="px-3.5 py-2.5 font-mono font-semibold text-slate-900 dark:text-slate-100">
                    {{ item.kaynakIfade }}
                  </td>
                  <td class="px-3.5 py-2.5 text-emerald-400 font-medium">
                    {{ item.okunus }}
                  </td>
                  <td class="px-3.5 py-2.5 text-slate-400">
                    {{ item.yalnizcaTamKelime ? 'Tam kelime' : 'Alt dizge' }} ·
                    {{ item.buyukKucukHarfDuyarli ? 'Harf duyarlı' : 'Duyarsız' }}
                  </td>
                  <td class="px-3.5 py-2.5 text-right space-x-2">
                    <button
                      type="button"
                      (click)="startEdit(item)"
                      class="text-brand-400 hover:underline font-medium"
                    >
                      Düzenle
                    </button>
                    <button
                      type="button"
                      (click)="deleteRule(item.id)"
                      class="text-rose-400 hover:underline font-medium"
                    >
                      Sil
                    </button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      } @else {
        <p class="text-xs text-slate-400">Henüz tanımlanmış bir telaffuz kuralı bulunmuyor.</p>
      }

      <!-- Canlı Önizleme Kutusu -->
      <div class="pt-2 border-t border-slate-200 dark:border-slate-800 space-y-2.5">
        <label class="block text-xs font-semibold text-slate-400">
          Canlı Telaffuz Önizleme
        </label>
        <div class="flex flex-col sm:flex-row gap-2">
          <input
            type="text"
            [(ngModel)]="previewInput"
            placeholder="Örnek cümle yazın (örn. Prof. Ahmet AI üzerine konuştu.)"
            class="flex-1 px-3.5 py-2 rounded-xl bg-slate-50 dark:bg-surface-900 border border-slate-300 dark:border-slate-700 text-sm"
          />
          <button
            type="button"
            (click)="runPreview()"
            class="px-4 py-2 min-h-[38px] rounded-xl bg-slate-200 dark:bg-slate-700 hover:bg-slate-300 dark:hover:bg-slate-600 text-xs font-semibold transition"
          >
            Önizle
          </button>
        </div>
        @if (previewResult(); as prev) {
          <div class="p-3 rounded-xl bg-emerald-500/10 border border-emerald-500/30 text-xs space-y-1">
            <p class="text-emerald-300 font-medium">{{ prev.donusturulmusMetin }}</p>
            <p class="text-[11px] text-slate-400">
              Uygulanan kural sayısı: {{ prev.uygulananKuralSayisi }}
            </p>
          </div>
        }
      </div>
    </section>
  `
})
export class PronunciationEditorComponent implements OnInit {
  private readonly pronunciationService = inject(PronunciationService);
  private readonly toast = inject(ToastService);

  readonly entries = signal<PronunciationDto[]>([]);
  readonly editingId = signal<string | null>(null);
  readonly previewResult = signal<PronunciationPreviewResponse | null>(null);

  kaynakIfade = '';
  okunus = '';
  yalnizcaTamKelime = true;
  buyukKucukHarfDuyarli = false;
  previewInput = '';

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.pronunciationService.getAll().subscribe({
      next: (list) => this.entries.set(list)
    });
  }

  saveRule(): void {
    const src = this.kaynakIfade.trim();
    const dst = this.okunus.trim();
    if (!src || !dst) {
      this.toast.warning('Kaynak ifade ve okunuş alanları zorunludur.');
      return;
    }

    const req = {
      kaynakIfade: src,
      okunus: dst,
      yalnizcaTamKelime: this.yalnizcaTamKelime,
      buyukKucukHarfDuyarli: this.buyukKucukHarfDuyarli
    };

    const editId = this.editingId();
    if (editId) {
      this.pronunciationService.update(editId, req).subscribe({
        next: () => {
          this.toast.success('Telaffuz kuralı güncellendi.');
          this.resetForm();
          this.load();
        }
      });
    } else {
      this.pronunciationService.create(req).subscribe({
        next: () => {
          this.toast.success('Telaffuz kuralı eklendi.');
          this.resetForm();
          this.load();
        }
      });
    }
  }

  startEdit(item: PronunciationDto): void {
    this.editingId.set(item.id);
    this.kaynakIfade = item.kaynakIfade;
    this.okunus = item.okunus;
    this.yalnizcaTamKelime = item.yalnizcaTamKelime;
    this.buyukKucukHarfDuyarli = item.buyukKucukHarfDuyarli;
  }

  resetForm(): void {
    this.editingId.set(null);
    this.kaynakIfade = '';
    this.okunus = '';
    this.yalnizcaTamKelime = true;
    this.buyukKucukHarfDuyarli = false;
  }

  deleteRule(id: string): void {
    this.pronunciationService.delete(id).subscribe({
      next: () => {
        this.toast.success('Kural silindi.');
        this.load();
      }
    });
  }

  runPreview(): void {
    const text = this.previewInput.trim();
    if (!text) return;
    this.pronunciationService.preview(text).subscribe({
      next: (res) => this.previewResult.set(res)
    });
  }
}

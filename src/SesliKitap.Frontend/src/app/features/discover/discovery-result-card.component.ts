import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { T } from '../../core/i18n/tr';
import { DiscoveryFileDto, DiscoveryResultDto } from '../../core/models/discovery.model';
import { FileSizePipe } from '../../shared/pipes/file-size.pipe';

@Component({
  selector: 'app-discovery-result-card',
  standalone: true,
  imports: [RouterLink, FileSizePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="rounded-2xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-800 p-4 flex flex-col justify-between gap-4 shadow-xs"
    >
      <div class="flex gap-4">
        <!-- Kapak veya Baş Harf Yer Tutucu -->
        <div
          class="w-20 h-28 shrink-0 rounded-xl overflow-hidden bg-slate-200 dark:bg-surface-900 flex items-center justify-center border border-slate-200 dark:border-slate-700"
        >
          @if (item().kapakUrl && !coverBroken()) {
            <img
              [src]="item().kapakUrl"
              [alt]="item().baslik"
              loading="lazy"
              (error)="coverBroken.set(true)"
              class="w-full h-full object-cover"
            />
          } @else {
            <span class="text-lg font-bold text-brand-400">
              {{ item().baslik.slice(0, 2).toUpperCase() }}
            </span>
          }
        </div>

        <div class="flex-1 min-w-0 space-y-1.5">
          <!-- Kaynak Rozetleri -->
          <div class="flex flex-wrap items-center gap-1.5">
            @for (src of item().kaynaklar; track src.saglayiciKodu + src.kaynakKimligi) {
              <span
                class="px-2 py-0.5 rounded-md text-[10px] font-semibold uppercase tracking-wide bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-300"
              >
                {{ src.saglayiciKodu }}
              </span>
            }
            @for (fmt of item().mevcutFormatlar; track fmt) {
              <span
                class="px-2 py-0.5 rounded-md text-[10px] font-bold uppercase bg-brand-500/15 text-brand-400"
              >
                {{ fmt }}
              </span>
            }
            @if (item().kutuphanedeVar) {
              <span
                class="px-2 py-0.5 rounded-md text-[10px] font-semibold bg-emerald-500/20 text-emerald-400"
              >
                {{ t.discover.inLibrary }}
              </span>
            }
          </div>

          <h3 class="text-sm font-bold text-slate-900 dark:text-slate-100 line-clamp-2">
            {{ item().baslik }}
          </h3>

          <p class="text-xs text-slate-500 dark:text-slate-400 truncate">
            {{ item().yazar || 'Bilinmeyen Yazar' }}
            @if (item().yayinYili) {
              <span> · {{ item().yayinYili }}</span>
            }
          </p>
        </div>
      </div>

      <!-- Dosya Listesi (Açıksa) -->
      @if (files().length > 0) {
        <div
          class="rounded-xl bg-slate-50 dark:bg-surface-900 p-2.5 space-y-1.5 border border-slate-200 dark:border-slate-700"
        >
          @for (f of files(); track f.indirmeUrl) {
            <div class="flex items-center justify-between gap-2 text-xs">
              <div class="min-w-0 truncate">
                <span class="font-bold text-brand-400 mr-1.5">[{{ f.format }}]</span>
                <span class="text-slate-700 dark:text-slate-300 truncate">{{ f.dosyaAdi }}</span>
                @if (f.boyutBayt) {
                  <span class="text-slate-400 ml-1">({{ f.boyutBayt | fileSize }})</span>
                }
              </div>
              <button
                type="button"
                [disabled]="importing()"
                (click)="importFile.emit(f)"
                class="shrink-0 px-2.5 py-1 rounded-lg bg-brand-500 hover:bg-brand-600 disabled:opacity-50 text-slate-950 font-semibold text-xs transition"
              >
                + Ekle
              </button>
            </div>
          }
        </div>
      }

      <!-- Alt Eylem Çubuğu -->
      <div class="flex items-center justify-between gap-2 pt-2 border-t border-slate-100 dark:border-slate-800">
        @if (item().kutuphanedeVar && item().mevcutBookId) {
          <a
            [routerLink]="['/kitap', item().mevcutBookId]"
            class="min-h-[38px] px-3.5 py-2 rounded-xl bg-emerald-500/15 text-emerald-400 hover:bg-emerald-500/25 text-xs font-semibold flex items-center gap-1.5 transition"
          >
            Kitaba Git →
          </a>
        } @else {
          <button
            type="button"
            [disabled]="loadingFiles() || importing()"
            (click)="requestFiles.emit()"
            class="min-h-[38px] px-3.5 py-2 rounded-xl bg-slate-100 dark:bg-slate-700 hover:bg-slate-200 dark:hover:bg-slate-600 text-xs font-semibold transition"
          >
            {{ loadingFiles() ? 'Dosyalar yükleniyor...' : t.discover.viewFiles }}
          </button>

          <button
            type="button"
            [disabled]="importing()"
            (click)="quickImport.emit()"
            class="min-h-[38px] px-4 py-2 rounded-xl bg-brand-500 hover:bg-brand-600 disabled:opacity-50 text-slate-950 text-xs font-semibold transition"
          >
            {{ importing() ? 'Ekleniyor...' : t.discover.addToLibrary }}
          </button>
        }
      </div>
    </div>
  `
})
export class DiscoveryResultCardComponent {
  readonly item = input.required<DiscoveryResultDto>();
  readonly files = input<DiscoveryFileDto[]>([]);
  readonly loadingFiles = input(false);
  readonly importing = input(false);

  readonly requestFiles = output<void>();
  readonly quickImport = output<void>();
  readonly importFile = output<DiscoveryFileDto>();

  readonly t = T;
  readonly coverBroken = signal(false);
}

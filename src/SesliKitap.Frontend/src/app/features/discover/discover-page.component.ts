import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { T } from '../../core/i18n/tr';
import {
  DiscoveryFileDto,
  DiscoveryProviderDto,
  DiscoveryResultDto
} from '../../core/models/discovery.model';
import { DiscoveryService } from '../../core/services/discovery.service';
import { ToastService } from '../../core/services/toast.service';
import { EmptyStateComponent } from '../../shared/components/empty-state.component';
import { DiscoveryResultCardComponent } from './discovery-result-card.component';

@Component({
  selector: 'app-discover-page',
  standalone: true,
  imports: [FormsModule, EmptyStateComponent, DiscoveryResultCardComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="max-w-7xl mx-auto px-4 md:px-6 py-6 space-y-6">
      <div>
        <h1 class="text-2xl font-bold text-slate-900 dark:text-slate-100">
          {{ t.discover.title }}
        </h1>
        <p class="text-xs text-slate-500 dark:text-slate-400 mt-1">
          Internet Archive, Open Library ve Project Gutenberg kataloglarında Türkçe kitap arayın ve tek tıkla kütüphanenize ekleyin.
        </p>
      </div>

      <!-- Telif Uyarısı Bandı (Web Araması seçiliyken) -->
      @if (isWebSelected()) {
        <div
          class="p-4 rounded-2xl bg-amber-500/15 border border-amber-500/30 text-amber-300 text-xs font-medium"
        >
          ⚠ {{ t.discover.webCopyrightWarning }}
        </div>
      }

      <!-- Arama Kutusu ve Sağlayıcı Çipleri -->
      <div
        class="rounded-2xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-800 p-4 sm:p-5 space-y-4"
      >
        <form (ngSubmit)="runSearch()" class="flex flex-col sm:flex-row gap-3">
          <input
            type="search"
            name="query"
            [ngModel]="query()"
            (ngModelChange)="query.set($event)"
            [placeholder]="t.discover.searchPlaceholder"
            class="flex-1 min-h-[44px] px-4 py-2.5 rounded-xl bg-slate-50 dark:bg-surface-900 border border-slate-200 dark:border-slate-700 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500"
          />
          <button
            type="submit"
            [disabled]="searching() || query().trim().length < 2"
            class="min-h-[44px] px-6 py-2.5 rounded-xl bg-brand-500 hover:bg-brand-600 disabled:opacity-50 text-slate-950 font-semibold text-sm transition"
          >
            {{ searching() ? 'Aranıyor...' : t.discover.searchButton }}
          </button>
        </form>

        @if (providers().length > 0) {
          <div class="flex flex-wrap items-center gap-2">
            <span class="text-xs text-slate-400 mr-1">Kaynaklar:</span>
            @for (p of providers(); track p.kod) {
              <button
                type="button"
                (click)="toggleProvider(p.kod)"
                class="min-h-[36px] px-3 py-1.5 rounded-xl text-xs font-semibold border transition"
                [class]="
                  selectedProviders().includes(p.kod)
                    ? 'bg-brand-500/15 border-brand-500 text-brand-400'
                    : 'bg-slate-50 dark:bg-surface-900 border-slate-200 dark:border-slate-700 text-slate-500'
                "
              >
                {{ p.ad }}
              </button>
            }
          </div>
        }
      </div>

      <!-- Sonuçlar -->
      @if (searching()) {
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          @for (i of [1, 2, 3, 4, 5, 6]; track i) {
            <div class="h-44 rounded-2xl bg-slate-200 dark:bg-surface-800 animate-pulse"></div>
          }
        </div>
      } @else if (!hasSearched()) {
        <app-empty-state
          [title]="t.discover.title"
          [description]="t.discover.empty"
        />
      } @else if (results().length === 0) {
        <app-empty-state
          [title]="t.discover.noResults"
          description="Farklı bir yazar adı veya anahtar kelime deneyebilirsiniz."
        />
      } @else {
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          @for (r of results(); track r.saglayiciKodu + ':' + r.kaynakKimligi) {
            <app-discovery-result-card
              [item]="r"
              [files]="filesMap()[cardKey(r)] ?? r.dosyalar"
              [loadingFiles]="loadingFilesKey() === cardKey(r)"
              [importing]="importingKey() === cardKey(r)"
              (requestFiles)="loadFilesFor(r)"
              (quickImport)="quickImport(r)"
              (importFile)="importSpecificFile(r, $event)"
            />
          }
        </div>
      }
    </div>
  `
})
export class DiscoverPageComponent implements OnInit {
  private readonly discoveryService = inject(DiscoveryService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  readonly t = T;
  readonly providers = signal<DiscoveryProviderDto[]>([]);
  readonly selectedProviders = signal<string[]>([]);
  readonly query = signal('');
  readonly searching = signal(false);
  readonly hasSearched = signal(false);
  readonly results = signal<DiscoveryResultDto[]>([]);

  readonly filesMap = signal<Record<string, DiscoveryFileDto[] | undefined>>({});
  readonly loadingFilesKey = signal<string | null>(null);
  readonly importingKey = signal<string | null>(null);

  readonly isWebSelected = computed(() => {
    const selected = this.selectedProviders();
    return this.providers().some(
      (p) => p.telifUyarisiGerekliMi && selected.includes(p.kod)
    );
  });

  ngOnInit(): void {
    this.discoveryService.getProviders().subscribe({
      next: (list) => {
        this.providers.set(list);
        this.selectedProviders.set(
          list.filter((p) => !p.telifUyarisiGerekliMi).map((p) => p.kod)
        );
      }
    });
  }

  toggleProvider(code: string): void {
    this.selectedProviders.update((curr) =>
      curr.includes(code) ? curr.filter((c) => c !== code) : [...curr, code]
    );
  }

  runSearch(): void {
    const q = this.query().trim();
    if (q.length < 2) return;

    this.searching.set(true);
    this.hasSearched.set(true);

    this.discoveryService.search(q, this.selectedProviders(), 1).subscribe({
      next: (items) => {
        this.results.set(items);
        this.searching.set(false);
      },
      error: () => {
        this.searching.set(false);
      }
    });
  }

  cardKey(item: DiscoveryResultDto): string {
    return `${item.saglayiciKodu}:${item.kaynakKimligi}`;
  }

  async loadFilesFor(item: DiscoveryResultDto): Promise<DiscoveryFileDto[]> {
    const key = this.cardKey(item);
    const existing = this.filesMap()[key] ?? item.dosyalar;
    if (existing.length > 0) {
      this.filesMap.update((m) => ({ ...m, [key]: existing }));
      return existing;
    }

    this.loadingFilesKey.set(key);
    try {
      const files = await firstValueFrom(
        this.discoveryService.getFiles(item.saglayiciKodu, item.kaynakKimligi)
      );
      this.filesMap.update((m) => ({ ...m, [key]: files }));
      if (files.length === 0) {
        this.toast.warning('Bu kaynakta doğrudan indirilebilir EPUB/PDF dosyası bulunamadı.');
      }
      return files;
    } finally {
      this.loadingFilesKey.set(null);
    }
  }

  async quickImport(item: DiscoveryResultDto): Promise<void> {
    const files = await this.loadFilesFor(item);
    if (files.length === 0) return;

    // EPUB tercih edilir, yoksa ilk dosya
    const chosen = files.find((f) => f.format === 'Epub') ?? files[0];
    await this.importSpecificFile(item, chosen);
  }

  async importSpecificFile(item: DiscoveryResultDto, file: DiscoveryFileDto): Promise<void> {
    const key = this.cardKey(item);
    this.importingKey.set(key);

    try {
      const created = await firstValueFrom(
        this.discoveryService.importBook({
          kaynakTipi: item.kaynakTipi,
          kaynakKimligi: item.kaynakKimligi,
          indirmeUrl: file.indirmeUrl,
          dosyaFormati: file.format,
          baslik: item.baslik,
          yazar: item.yazar,
          yayinYili: item.yayinYili,
          kapakUrl: item.kapakUrl
        })
      );
      this.toast.success('Kitap kütüphanenize eklendi ve indirme başlatıldı.');
      void this.router.navigate(['/kitap', created.id]);
    } finally {
      this.importingKey.set(null);
    }
  }
}

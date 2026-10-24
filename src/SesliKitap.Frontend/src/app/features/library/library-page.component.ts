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
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { T } from '../../core/i18n/tr';
import { BookListDto } from '../../core/models/book.model';
import { KitapDurumu } from '../../core/models/enums';
import { BookService } from '../../core/services/book.service';
import { SignalrService } from '../../core/services/signalr.service';
import { CoverImageComponent } from '../../shared/components/cover-image.component';
import { EmptyStateComponent } from '../../shared/components/empty-state.component';
import { ProgressBarComponent } from '../../shared/components/progress-bar.component';
import { DropZoneDirective } from '../../shared/directives/drop-zone.directive';
import { BookCardComponent } from './book-card.component';
import { UploadDialogComponent } from './upload-dialog.component';

@Component({
  selector: 'app-library-page',
  standalone: true,
  imports: [
    FormsModule,
    RouterLink,
    DropZoneDirective,
    BookCardComponent,
    UploadDialogComponent,
    EmptyStateComponent,
    CoverImageComponent,
    ProgressBarComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      appDropZone
      (dropped)="onPageDrop($event)"
      class="max-w-7xl mx-auto px-4 md:px-6 py-6 space-y-8"
    >
      <!-- Dinlemeye Devam Et Rafı -->
      @if (continueBooks().length > 0) {
        <section>
          <h2 class="text-base font-bold text-slate-900 dark:text-slate-100 mb-3">
            {{ t.library.continue }}
          </h2>
          <div class="flex gap-4 overflow-x-auto pb-2">
            @for (item of continueBooks(); track item.id) {
              <div
                class="min-w-[260px] max-w-[300px] flex items-center gap-3 p-3 rounded-2xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-800 shadow-xs"
              >
                <div class="w-14 h-20 shrink-0">
                  <app-cover-image
                    [bookId]="item.id"
                    [title]="item.baslik"
                    [hasCover]="item.kapakVar"
                  />
                </div>
                <div class="flex-1 min-w-0">
                  <a
                    [routerLink]="['/kitap', item.id]"
                    class="text-sm font-semibold text-slate-900 dark:text-slate-100 truncate block hover:text-brand-400"
                  >
                    {{ item.baslik }}
                  </a>
                  <p class="text-xs text-slate-500 dark:text-slate-400 truncate">
                    {{ item.yazar || 'Bilinmeyen Yazar' }}
                  </p>
                  <div class="mt-2">
                    <app-progress-bar [value]="item.tamamlanmaYuzdesi ?? 0" variant="emerald" />
                  </div>
                  <a
                    [routerLink]="['/dinle', item.id]"
                    class="mt-2 inline-flex items-center gap-1 text-xs font-semibold text-brand-400 hover:text-brand-300"
                  >
                    ▶ {{ t.bookDetail.listen }}
                  </a>
                </div>
              </div>
            }
          </div>
        </section>
      }

      <!-- Başlık, Arama, Filtre ve Yükleme -->
      <section class="space-y-4">
        <div class="flex flex-wrap items-center justify-between gap-3">
          <h1 class="text-2xl font-bold text-slate-900 dark:text-slate-100">
            {{ t.library.title }}
          </h1>
          <button
            type="button"
            (click)="uploadDialogOpen.set(true)"
            class="min-h-[44px] px-4 py-2.5 rounded-xl bg-brand-500 hover:bg-brand-600 text-slate-950 font-semibold text-sm transition flex items-center gap-2"
          >
            <span>+</span>
            <span>{{ t.library.upload }}</span>
          </button>
        </div>

        <div class="flex flex-col sm:flex-row gap-3">
          <input
            type="search"
            [ngModel]="searchTerm()"
            (ngModelChange)="onSearchChange($event)"
            [placeholder]="t.library.searchPlaceholder"
            class="flex-1 px-4 py-2.5 min-h-[44px] rounded-xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-800 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500"
          />

          <select
            [ngModel]="selectedStatus()"
            (ngModelChange)="onStatusChange($event)"
            aria-label="Durum filtresi"
            class="px-3.5 py-2.5 min-h-[44px] rounded-xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-800 text-sm"
          >
            <option value="">{{ t.library.allStatuses }}</option>
            <option value="Hazir">{{ t.status.Hazir }}</option>
            <option value="KismenHazir">{{ t.status.KismenHazir }}</option>
            <option value="Seslendiriliyor">{{ t.status.Seslendiriliyor }}</option>
            <option value="OnizlemeBekliyor">{{ t.status.OnizlemeBekliyor }}</option>
            <option value="Isleniyor">{{ t.status.Isleniyor }}</option>
            <option value="OcrGerekli">{{ t.status.OcrGerekli }}</option>
            <option value="Hata">{{ t.status.Hata }}</option>
          </select>

          <select
            [ngModel]="selectedSort()"
            (ngModelChange)="onSortChange($event)"
            aria-label="Sıralama"
            class="px-3.5 py-2.5 min-h-[44px] rounded-xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-800 text-sm"
          >
            <option value="yeni">{{ t.library.sortNewest }}</option>
            <option value="sonDinlenen">{{ t.library.sortRecentListen }}</option>
            <option value="baslik">{{ t.library.sortTitle }}</option>
          </select>
        </div>

        @if (loading()) {
          <div class="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-4">
            @for (idx of [1, 2, 3, 4, 5]; track idx) {
              <div
                class="aspect-[3/4] rounded-2xl bg-slate-200 dark:bg-surface-800 animate-pulse"
              ></div>
            }
          </div>
        } @else if (books().length === 0) {
          <app-empty-state
            [title]="t.library.empty"
            [description]="t.library.dropHint"
            [actionLabel]="t.library.upload"
            (actionClick)="uploadDialogOpen.set(true)"
          />
        } @else {
          <div class="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-4">
            @for (book of books(); track book.id) {
              <app-book-card
                [book]="book"
                [liveProgress]="signalr.bookProcessingProgress()[book.id]?.yuzde ?? null"
              />
            }
          </div>
        }
      </section>

      <app-upload-dialog
        [open]="uploadDialogOpen()"
        [initialFile]="droppedFile()"
        (closed)="uploadDialogOpen.set(false); droppedFile.set(null)"
        (uploaded)="loadBooks()"
      />
    </div>
  `
})
export class LibraryPageComponent implements OnInit {
  private readonly bookService = inject(BookService);
  readonly signalr = inject(SignalrService);
  private readonly destroyRef = inject(DestroyRef);

  readonly t = T;
  readonly books = signal<BookListDto[]>([]);
  readonly continueBooks = signal<BookListDto[]>([]);
  readonly loading = signal<boolean>(true);

  readonly searchTerm = signal('');
  readonly selectedStatus = signal<KitapDurumu | ''>('');
  readonly selectedSort = signal('yeni');

  readonly uploadDialogOpen = signal(false);
  readonly droppedFile = signal<File | null>(null);

  private readonly search$ = new Subject<string>();

  ngOnInit(): void {
    this.loadBooks();
    this.loadContinue();

    void this.signalr.joinLibrary();
    this.destroyRef.onDestroy(() => {
      void this.signalr.leaveLibrary();
    });

    this.search$
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe((term) => {
        this.searchTerm.set(term);
        this.loadBooks();
      });

    this.signalr.bookStatusChanged$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((msg) => {
        const exists = this.books().some((b) => b.id === msg.bookId);
        if (!exists) {
          this.loadBooks();
          return;
        }
        this.books.update((list) =>
          list.map((b) =>
            b.id === msg.bookId
              ? {
                  ...b,
                  durum: msg.durum,
                  bolumSayisi: msg.bolumSayisi,
                  hazirBolumSayisi: msg.hazirBolumSayisi
                }
              : b
          )
        );
      });
  }

  loadBooks(): void {
    this.loading.set(true);
    this.bookService
      .getList({
        arama: this.searchTerm(),
        durum: this.selectedStatus(),
        siralama: this.selectedSort(),
        sayfa: 1,
        boyut: 40
      })
      .subscribe({
        next: (res) => {
          this.books.set(res.kayitlar);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        }
      });
  }

  loadContinue(): void {
    this.bookService.getContinue(8).subscribe({
      next: (list) => this.continueBooks.set(list),
      error: () => this.continueBooks.set([])
    });
  }

  onSearchChange(value: string): void {
    this.search$.next(value);
  }

  onStatusChange(status: KitapDurumu | ''): void {
    this.selectedStatus.set(status);
    this.loadBooks();
  }

  onSortChange(sort: string): void {
    this.selectedSort.set(sort);
    this.loadBooks();
  }

  onPageDrop(files: File[]): void {
    if (files.length > 0) {
      this.droppedFile.set(files[0]);
      this.uploadDialogOpen.set(true);
    }
  }
}

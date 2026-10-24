import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { T } from '../../core/i18n/tr';
import { ChapterSummary } from '../../core/models/chapter.model';
import { ChapterService } from '../../core/services/chapter.service';
import { ToastService } from '../../core/services/toast.service';
import { StatusBadgeComponent } from '../../shared/components/status-badge.component';
import { DurationPipe } from '../../shared/pipes/duration.pipe';

@Component({
  selector: 'app-chapter-list',
  standalone: true,
  imports: [DragDropModule, RouterLink, StatusBadgeComponent, DurationPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-4">
      <!-- Çoklu Seçim Araç Çubuğu -->
      @if (selectedCount() > 0) {
        <div
          class="sticky top-16 z-20 p-3 rounded-xl bg-brand-500/15 border border-brand-500/30 backdrop-blur-md flex flex-wrap items-center justify-between gap-2"
        >
          <span class="text-xs font-semibold text-brand-300">
            {{ selectedCount() }} bölüm seçildi
          </span>
          <div class="flex flex-wrap items-center gap-2">
            @if (canMergeSelected()) {
              <button
                type="button"
                (click)="mergeSelected()"
                class="px-3 py-1.5 min-h-[36px] rounded-lg bg-slate-800 hover:bg-slate-700 text-xs font-semibold text-slate-100"
              >
                {{ t.bookDetail.mergeSelected }}
              </button>
            }
            <button
              type="button"
              (click)="bulkSetInclusion(true)"
              class="px-3 py-1.5 min-h-[36px] rounded-lg bg-slate-800 hover:bg-slate-700 text-xs font-semibold text-slate-100"
            >
              {{ t.bookDetail.includeSelected }}
            </button>
            <button
              type="button"
              (click)="bulkSetInclusion(false)"
              class="px-3 py-1.5 min-h-[36px] rounded-lg bg-slate-800 hover:bg-slate-700 text-xs font-semibold text-slate-100"
            >
              {{ t.bookDetail.excludeSelected }}
            </button>
            <button
              type="button"
              (click)="synthesizeSelected.emit(selectedArray())"
              class="px-3 py-1.5 min-h-[36px] rounded-lg bg-brand-500 hover:bg-brand-600 text-xs font-semibold text-slate-950"
            >
              {{ t.bookDetail.synthesizeSelected }}
            </button>
            <button
              type="button"
              (click)="clearSelection()"
              class="px-2.5 py-1.5 text-xs text-slate-400 hover:text-slate-200"
            >
              Temizle
            </button>
          </div>
        </div>
      }

      <!-- Bölüm Listesi -->
      <div
        cdkDropList
        [cdkDropListDisabled]="isLocked()"
        (cdkDropListDropped)="onDrop($event)"
        class="divide-y divide-slate-200 dark:divide-slate-800 rounded-2xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-800 overflow-hidden"
      >
        @for (ch of chapters(); track ch.id) {
          <div
            cdkDrag
            class="p-3.5 sm:px-5 flex items-center gap-3 hover:bg-slate-50 dark:hover:bg-slate-800/60 transition"
            [class.opacity-50]="!ch.dahilMi"
          >
            <button
              type="button"
              cdkDragHandle
              [disabled]="isLocked()"
              class="cursor-grab text-slate-400 hover:text-slate-200 px-1 select-none"
              title="Sıralamak için sürükleyin"
            >
              ⋮⋮
            </button>

            <input
              type="checkbox"
              [checked]="isSelected(ch.id)"
              (change)="toggleSelect(ch.id)"
              class="w-4 h-4 rounded border-slate-600 text-brand-500 focus:ring-brand-500"
              [attr.aria-label]="ch.baslik"
            />

            <span class="w-8 text-xs font-mono text-slate-400 text-right shrink-0">
              #{{ ch.siraNo }}
            </span>

            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2">
                <p class="text-sm font-semibold text-slate-900 dark:text-slate-100 truncate">
                  {{ ch.baslik }}
                </p>
                @if (ch.otomatikHaricNedeni) {
                  <span
                    class="px-2 py-0.5 rounded text-[10px] bg-slate-700 text-slate-300"
                    [title]="ch.otomatikHaricNedeni"
                  >
                    Otomatik Hariç: {{ ch.otomatikHaricNedeni }}
                  </span>
                }
              </div>
              <p class="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
                {{ ch.kelimeSayisi }} kelime ·
                {{ (ch.sureSn ?? ch.tahminiSureSn) | duration }}
                @if (ch.baslangicSayfasi) {
                  <span> · s. {{ ch.baslangicSayfasi }}–{{ ch.bitisSayfasi }}</span>
                }
              </p>
            </div>

            <app-status-badge [status]="ch.sesDurumu" kind="chapter" />

            <div class="flex items-center gap-1.5">
              <button
                type="button"
                (click)="toggleSingleInclusion(ch)"
                class="min-w-[38px] min-h-[38px] px-2.5 rounded-lg text-xs font-medium border border-slate-200 dark:border-slate-700 hover:bg-slate-100 dark:hover:bg-slate-700"
                [title]="ch.dahilMi ? 'Seslendirmeden hariç tut' : 'Seslendirmeye dahil et'"
              >
                {{ ch.dahilMi ? 'Dahil' : 'Hariç' }}
              </button>

              @if (ch.sesDurumu === 'Hazir' || ch.sesDurumu === 'GuncelDegil') {
                <a
                  [routerLink]="['/dinle', bookId()]"
                  [queryParams]="{ bolum: ch.id }"
                  class="min-w-[38px] min-h-[38px] rounded-lg bg-brand-500/15 hover:bg-brand-500/30 text-brand-400 flex items-center justify-center text-xs font-bold"
                  title="Bu bölümden dinle"
                >
                  ▶
                </a>
              }

              <button
                type="button"
                (click)="editChapter.emit(ch.id)"
                class="min-w-[38px] min-h-[38px] rounded-lg hover:bg-slate-100 dark:hover:bg-slate-700 text-slate-400 hover:text-slate-100 flex items-center justify-center text-sm"
                title="Bölüm metnini düzenle"
              >
                ✎
              </button>
            </div>
          </div>
        }
      </div>
    </div>
  `
})
export class ChapterListComponent {
  private readonly chapterService = inject(ChapterService);
  private readonly toast = inject(ToastService);

  readonly bookId = input.required<string>();
  readonly chapters = input.required<ChapterSummary[]>();
  readonly isLocked = input<boolean>(false);

  readonly chaptersUpdated = output<ChapterSummary[]>();
  readonly editChapter = output<string>();
  readonly synthesizeSelected = output<string[]>();

  readonly t = T;
  readonly selectedIds = signal<Set<string>>(new Set());

  readonly selectedCount = computed(() => this.selectedIds().size);
  readonly selectedArray = computed(() => Array.from(this.selectedIds()));

  readonly canMergeSelected = computed(() => {
    const ids = this.selectedIds();
    if (ids.size < 2) return false;
    const selectedChaps = this.chapters()
      .filter((c) => ids.has(c.id))
      .sort((a, b) => a.siraNo - b.siraNo);
    for (let i = 1; i < selectedChaps.length; i++) {
      if (selectedChaps[i].siraNo !== selectedChaps[i - 1].siraNo + 1) {
        return false;
      }
    }
    return true;
  });

  isSelected(id: string): boolean {
    return this.selectedIds().has(id);
  }

  toggleSelect(id: string): void {
    this.selectedIds.update((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  clearSelection(): void {
    this.selectedIds.set(new Set());
  }

  toggleSingleInclusion(ch: ChapterSummary): void {
    this.chapterService.setInclusion(this.bookId(), [ch.id], !ch.dahilMi).subscribe((updated) => {
      this.chaptersUpdated.emit(updated);
    });
  }

  bulkSetInclusion(dahilMi: boolean): void {
    const ids = this.selectedArray();
    if (ids.length === 0) return;

    this.chapterService.setInclusion(this.bookId(), ids, dahilMi).subscribe((updated) => {
      this.chaptersUpdated.emit(updated);
      this.clearSelection();
      this.toast.success(
        dahilMi ? 'Seçili bölümler dahil edildi.' : 'Seçili bölümler hariç tutuldu.'
      );
    });
  }

  mergeSelected(): void {
    const ids = this.selectedArray();
    if (!this.canMergeSelected()) {
      this.toast.warning('Yalnızca ardışık bölümler birleştirilebilir.');
      return;
    }

    this.chapterService.merge(this.bookId(), ids).subscribe((updated) => {
      this.chaptersUpdated.emit(updated);
      this.clearSelection();
      this.toast.success('Seçili bölümler birleştirildi.');
    });
  }

  onDrop(event: CdkDragDrop<ChapterSummary[]>): void {
    if (event.previousIndex === event.currentIndex || this.isLocked()) {
      return;
    }

    const copy = [...this.chapters()];
    moveItemInArray(copy, event.previousIndex, event.currentIndex);
    const orderedIds = copy.map((c) => c.id);

    this.chapterService.reorder(this.bookId(), orderedIds).subscribe((updated) => {
      this.chaptersUpdated.emit(updated);
    });
  }
}

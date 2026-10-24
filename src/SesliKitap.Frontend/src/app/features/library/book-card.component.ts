import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { T } from '../../core/i18n/tr';
import { BookListDto } from '../../core/models/book.model';
import { CoverImageComponent } from '../../shared/components/cover-image.component';
import { ProgressBarComponent } from '../../shared/components/progress-bar.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge.component';
import { DurationPipe } from '../../shared/pipes/duration.pipe';

@Component({
  selector: 'app-book-card',
  standalone: true,
  imports: [RouterLink, CoverImageComponent, StatusBadgeComponent, ProgressBarComponent, DurationPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a
      [routerLink]="['/kitap', book().id]"
      class="group flex flex-col rounded-2xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-800 hover:border-brand-500/50 overflow-hidden shadow-xs hover:shadow-md transition p-3"
    >
      <div class="relative aspect-[3/4] w-full mb-3">
        <app-cover-image
          [bookId]="book().id"
          [title]="book().baslik"
          [hasCover]="book().kapakVar"
        />
        <div class="absolute top-2 left-2">
          <app-status-badge [status]="book().durum" kind="book" />
        </div>
      </div>

      <h3
        class="font-semibold text-sm text-slate-900 dark:text-slate-100 line-clamp-2 group-hover:text-brand-400 transition"
      >
        {{ book().baslik }}
      </h3>
      <p class="mt-0.5 text-xs text-slate-500 dark:text-slate-400 truncate">
        {{ book().yazar || 'Bilinmeyen Yazar' }}
      </p>

      @if (processingPercent() !== null) {
        <div class="mt-2.5">
          <app-progress-bar [value]="processingPercent()!" />
        </div>
      }

      <div
        class="mt-auto pt-3 flex items-center justify-between text-[11px] text-slate-500 dark:text-slate-400"
      >
        <span>{{ book().hazirBolumSayisi }}/{{ book().bolumSayisi }} {{ t.library.chaptersReady }}</span>
        @if (book().toplamSureSn > 0) {
          <span>{{ book().toplamSureSn | duration }}</span>
        }
      </div>

      @if (book().tamamlanmaYuzdesi !== null && book().tamamlanmaYuzdesi! > 0) {
        <div class="mt-2">
          <app-progress-bar [value]="book().tamamlanmaYuzdesi!" variant="emerald" />
        </div>
      }
    </a>
  `
})
export class BookCardComponent {
  readonly book = input.required<BookListDto>();
  readonly liveProgress = input<number | null>(null);
  readonly t = T;

  readonly processingPercent = computed(() => {
    const live = this.liveProgress();
    if (live !== null && live !== undefined) {
      return live;
    }
    const status = this.book().durum;
    if (status === 'Isleniyor' || status === 'Indiriliyor' || status === 'Seslendiriliyor') {
      return 15;
    }
    return null;
  });
}

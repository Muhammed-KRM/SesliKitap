import { ChangeDetectionStrategy, Component, effect, input, output, signal } from '@angular/core';
import { T } from '../../core/i18n/tr';
import { ChapterSummary } from '../../core/models/chapter.model';
import { BookmarkDto } from '../../core/models/faz8.model';
import { StatusBadgeComponent } from '../../shared/components/status-badge.component';
import { ClockPipe } from '../../shared/pipes/clock.pipe';
import { DurationPipe } from '../../shared/pipes/duration.pipe';

@Component({
  selector: 'app-chapter-drawer',
  standalone: true,
  imports: [StatusBadgeComponent, DurationPipe, ClockPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (open()) {
      <div
        class="fixed inset-0 z-50 bg-slate-950/75 backdrop-blur-sm flex justify-end"
        (click)="closed.emit()"
      >
        <div
          class="w-full max-w-md bg-white dark:bg-surface-900 h-full flex flex-col border-l border-slate-200 dark:border-slate-800 shadow-2xl"
          (click)="$event.stopPropagation()"
        >
          <div
            class="px-5 py-3 border-b border-slate-200 dark:border-slate-800 flex items-center justify-between"
          >
            <div class="flex items-center gap-2">
              <button
                type="button"
                (click)="activeTab.set('chapters')"
                class="px-3 py-1.5 rounded-xl text-xs font-bold transition"
                [class]="
                  activeTab() === 'chapters'
                    ? 'bg-brand-500 text-slate-950'
                    : 'text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800'
                "
              >
                {{ t.player.chapters }} ({{ chapters().length }})
              </button>
              <button
                type="button"
                (click)="activeTab.set('bookmarks')"
                class="px-3 py-1.5 rounded-xl text-xs font-bold transition"
                [class]="
                  activeTab() === 'bookmarks'
                    ? 'bg-brand-500 text-slate-950'
                    : 'text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800'
                "
              >
                🔖 {{ t.player.bookmarks }} ({{ bookmarks().length }})
              </button>
            </div>
            <button
              type="button"
              (click)="closed.emit()"
              class="min-w-[40px] min-h-[40px] rounded-xl hover:bg-slate-100 dark:hover:bg-slate-800 text-slate-400"
            >
              ✕
            </button>
          </div>

          @if (activeTab() === 'chapters') {
            <div class="flex-1 overflow-y-auto divide-y divide-slate-200 dark:divide-slate-800">
              @for (ch of chapters(); track ch.id) {
                @let playable =
                  ch.dahilMi && (ch.sesDurumu === 'Hazir' || ch.sesDurumu === 'GuncelDegil');
                <button
                  type="button"
                  [disabled]="!playable"
                  (click)="selectChapter.emit(ch.id); closed.emit()"
                  class="w-full p-4 text-left flex items-center justify-between gap-3 transition disabled:opacity-40 disabled:cursor-not-allowed"
                  [class]="
                    currentChapterId() === ch.id
                      ? 'bg-brand-500/15 border-l-4 border-brand-500'
                      : 'hover:bg-slate-50 dark:hover:bg-slate-800/60'
                  "
                >
                  <div class="min-w-0 flex-1">
                    <p class="text-sm font-semibold text-slate-900 dark:text-slate-100 truncate">
                      #{{ ch.siraNo }} · {{ ch.baslik }}
                    </p>
                    <p class="text-xs text-slate-400 mt-0.5">
                      {{ (ch.sureSn ?? ch.tahminiSureSn) | duration }}
                    </p>
                  </div>
                  <app-status-badge [status]="ch.sesDurumu" kind="chapter" />
                </button>
              }
            </div>
          } @else {
            <div class="flex-1 overflow-y-auto divide-y divide-slate-200 dark:divide-slate-800">
              @if (bookmarks().length === 0) {
                <div class="p-8 text-center text-xs text-slate-400">
                  Bu kitap için henüz yer imi eklenmemiş.
                </div>
              } @else {
                @for (bm of bookmarks(); track bm.id) {
                  <div
                    class="p-4 hover:bg-slate-50 dark:hover:bg-slate-800/60 flex items-start justify-between gap-3 transition cursor-pointer"
                    (click)="selectBookmark.emit(bm); closed.emit()"
                  >
                    <div class="min-w-0 flex-1 space-y-1">
                      <div class="flex items-center gap-2">
                        <span class="text-xs font-bold text-brand-400">
                          #{{ bm.bolumSiraNo }} · {{ bm.pozisyonSn | clock }}
                        </span>
                        <span class="text-xs text-slate-400 truncate">{{ bm.bolumBaslik }}</span>
                      </div>
                      @if (bm.notMetni) {
                        <p class="text-xs font-semibold text-slate-900 dark:text-slate-100">
                          📝 {{ bm.notMetni }}
                        </p>
                      }
                      @if (bm.cumleMetni) {
                        <p class="text-[11px] text-slate-400 italic line-clamp-2">
                          "{{ bm.cumleMetni }}"
                        </p>
                      }
                    </div>
                    <button
                      type="button"
                      (click)="deleteBookmark.emit(bm.id); $event.stopPropagation()"
                      class="p-1.5 rounded-lg text-rose-400 hover:bg-rose-500/10 text-xs"
                      title="Yer imini sil"
                    >
                      🗑
                    </button>
                  </div>
                }
              }
            </div>
          }
        </div>
      </div>
    }
  `
})
export class ChapterDrawerComponent {
  readonly open = input<boolean>(false);
  readonly chapters = input<ChapterSummary[]>([]);
  readonly bookmarks = input<BookmarkDto[]>([]);
  readonly currentChapterId = input<string | null>(null);
  readonly initialTab = input<'chapters' | 'bookmarks'>('chapters');

  readonly selectChapter = output<string>();
  readonly selectBookmark = output<BookmarkDto>();
  readonly deleteBookmark = output<string>();
  readonly closed = output<void>();

  readonly activeTab = signal<'chapters' | 'bookmarks'>('chapters');
  readonly t = T;

  constructor() {
    effect(
      () => {
        if (this.open()) {
          this.activeTab.set(this.initialTab());
        }
      },
      { allowSignalWrites: true }
    );
  }
}

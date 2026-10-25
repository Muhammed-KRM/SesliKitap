import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  input,
  signal
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { T } from '../../core/i18n/tr';
import { BookmarkDto } from '../../core/models/faz8.model';
import { AudioFormatService } from '../../core/services/audio-format.service';
import { AudioPlayerService } from '../../core/services/audio-player.service';
import { BookmarkService } from '../../core/services/bookmark.service';
import { DownloadManagerService } from '../../core/services/download-manager.service';
import { ToastService } from '../../core/services/toast.service';
import { CoverImageComponent } from '../../shared/components/cover-image.component';
import { ChapterDrawerComponent } from './chapter-drawer.component';
import { SeekBarComponent } from './seek-bar.component';
import { SentenceReaderComponent } from './sentence-reader.component';
import { SleepTimerMenuComponent } from './sleep-timer-menu.component';
import { SpeedMenuComponent } from './speed-menu.component';

@Component({
  selector: 'app-player-page',
  standalone: true,
  imports: [
    FormsModule,
    RouterLink,
    CoverImageComponent,
    SeekBarComponent,
    SpeedMenuComponent,
    SleepTimerMenuComponent,
    ChapterDrawerComponent,
    SentenceReaderComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="max-w-7xl mx-auto px-4 md:px-6 py-6">
      @if (player.book(); as b) {
        <div class="mb-4 flex items-center justify-between">
          <a
            [routerLink]="['/kitap', b.id]"
            class="text-xs font-semibold text-slate-400 hover:text-brand-400 flex items-center gap-1.5 transition"
          >
            ← Kitap Detayına Dön
          </a>

          <button
            type="button"
            (click)="showText.set(!showText())"
            class="px-3 py-1.5 rounded-xl bg-slate-100 dark:bg-slate-800 text-xs font-semibold text-slate-700 dark:text-slate-200"
          >
            {{ showText() ? t.player.hideText : t.player.showText }}
          </button>
        </div>

        <div
          class="grid grid-cols-1 gap-6 items-start"
          [class.lg:grid-cols-12]="showText()"
        >
          <!-- Sol Sütun: Kapak + Oynatıcı Kontrolleri -->
          <div
            class="rounded-2xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-800 p-6 space-y-6"
            [class.lg:col-span-5]="showText()"
            [class.max-w-xl]="!showText()"
            [class.mx-auto]="!showText()"
            [class.w-full]="!showText()"
          >
            <div class="w-44 sm:w-56 aspect-[3/4] mx-auto rounded-2xl overflow-hidden shadow-xl">
              <app-cover-image [bookId]="b.id" [title]="b.baslik" [hasCover]="b.kapakVar" />
            </div>

            <div class="text-center space-y-1">
              <h1 class="text-lg font-bold text-slate-900 dark:text-slate-100 line-clamp-2">
                {{ b.baslik }}
              </h1>
              <p class="text-xs text-slate-400">{{ b.yazar || 'Bilinmeyen Yazar' }}</p>
              @if (player.chapter(); as ch) {
                <p class="text-sm font-semibold text-brand-400 pt-1">
                  #{{ ch.siraNo }} · {{ ch.baslik }}
                </p>
              }
            </div>

            <app-seek-bar
              [position]="player.position()"
              [duration]="player.duration()"
              [bufferedEnd]="player.bufferedEnd()"
              [playbackRate]="player.rate()"
              (seekChange)="player.seek($event)"
            />

            <!-- Ana Oynatma Kontrolleri -->
            <div class="flex items-center justify-center gap-3 sm:gap-5">
              <button
                type="button"
                (click)="player.prevChapter()"
                class="min-w-[44px] min-h-[44px] rounded-xl bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-700 dark:text-slate-200 font-bold flex items-center justify-center transition"
                [title]="t.player.prevChapter"
              >
                ⏮
              </button>

              <button
                type="button"
                (click)="player.skip(-formatService.skipBackSec())"
                class="min-w-[48px] min-h-[48px] px-3 rounded-xl bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-xs font-bold text-slate-800 dark:text-slate-100 transition"
                [title]="t.player.skipBackward"
              >
                ↺{{ formatService.skipBackSec() }}
              </button>

              <button
                type="button"
                (click)="player.toggle()"
                class="w-16 h-16 rounded-full bg-brand-500 hover:bg-brand-600 text-slate-950 text-2xl font-bold shadow-lg shadow-brand-500/25 flex items-center justify-center transition"
                [attr.aria-label]="player.isPlaying() ? t.player.pause : t.player.play"
              >
                @if (player.isBuffering()) {
                  <span class="animate-spin text-lg">⏳</span>
                } @else {
                  <span>{{ player.isPlaying() ? '⏸' : '▶' }}</span>
                }
              </button>

              <button
                type="button"
                (click)="player.skip(formatService.skipForwardSec())"
                class="min-w-[48px] min-h-[48px] px-3 rounded-xl bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-xs font-bold text-slate-800 dark:text-slate-100 transition"
                [title]="t.player.skipForward"
              >
                ↻{{ formatService.skipForwardSec() }}
              </button>

              <button
                type="button"
                (click)="player.nextChapter()"
                class="min-w-[44px] min-h-[44px] rounded-xl bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-700 dark:text-slate-200 font-bold flex items-center justify-center transition"
                [title]="t.player.nextChapter"
              >
                ⏭
              </button>
            </div>

            <!-- Alt Menü Sırası: Hız, Uyku, Yer İmi, Bölümler, Çevrimdışı İndir -->
            <div class="pt-2 border-t border-slate-200 dark:border-slate-800 flex flex-wrap items-center justify-between gap-2">
              <div class="flex items-center gap-2">
                <app-speed-menu
                  [rate]="player.rate()"
                  (rateChange)="player.setRate($event)"
                />
                <app-sleep-timer-menu />
                <button
                  type="button"
                  (click)="addingBookmark.set(!addingBookmark())"
                  class="min-h-[44px] px-3 py-2 rounded-xl bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-xs font-semibold text-slate-700 dark:text-slate-200 transition"
                  [title]="t.player.addBookmark"
                >
                  🔖 {{ t.player.addBookmark }}
                </button>
              </div>

              <div class="flex items-center gap-2">
                <button
                  type="button"
                  (click)="openDrawer('chapters')"
                  class="min-h-[44px] px-3.5 py-2 rounded-xl bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-xs font-semibold text-slate-700 dark:text-slate-200 transition"
                >
                  📑 {{ t.player.chapters }}
                </button>
                <button
                  type="button"
                  (click)="openDrawer('bookmarks')"
                  class="min-h-[44px] px-3 py-2 rounded-xl bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-xs font-semibold text-slate-700 dark:text-slate-200 transition"
                >
                  🔖 ({{ bookmarks().length }})
                </button>
                <button
                  type="button"
                  (click)="downloadBook(b.id)"
                  class="min-h-[44px] px-3.5 py-2 rounded-xl bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-xs font-semibold text-slate-700 dark:text-slate-200 transition"
                  title="Çevrimdışı dinleme için indir"
                >
                  ⬇ İndir
                </button>
              </div>
            </div>

            <!-- Hızlı Yer İmi Ekleme Kutusu -->
            @if (addingBookmark()) {
              <div class="p-3.5 rounded-xl bg-slate-50 dark:bg-surface-900 border border-slate-200 dark:border-slate-800 space-y-2.5">
                <input
                  type="text"
                  [(ngModel)]="bookmarkNote"
                  placeholder="İsteğe bağlı yer imi notu..."
                  class="w-full px-3 py-2 rounded-xl bg-white dark:bg-surface-800 border border-slate-300 dark:border-slate-700 text-xs"
                />
                <div class="flex items-center justify-end gap-2">
                  <button
                    type="button"
                    (click)="addingBookmark.set(false)"
                    class="px-3 py-1.5 rounded-lg text-xs text-slate-400 hover:bg-slate-200 dark:hover:bg-slate-800"
                  >
                    {{ t.common.cancel }}
                  </button>
                  <button
                    type="button"
                    (click)="saveBookmark(b.id)"
                    class="px-3.5 py-1.5 rounded-lg bg-brand-500 text-slate-950 text-xs font-semibold"
                  >
                    {{ t.common.save }}
                  </button>
                </div>
              </div>
            }
          </div>

          <!-- Sağ Sütun: Canlı Cümle Takibi (Karaoke) -->
          @if (showText()) {
            <div class="lg:col-span-7 h-[520px] lg:h-[640px]">
              <app-sentence-reader
                [timestamps]="player.timestamps()"
                [activeSentenceIndex]="player.currentSentence()?.i ?? null"
                (sentenceClicked)="player.seekToSentence($event)"
              />
            </div>
          }
        </div>

        <app-chapter-drawer
          [open]="drawerOpen()"
          [chapters]="b.bolumler"
          [bookmarks]="bookmarks()"
          [initialTab]="drawerTab()"
          [currentChapterId]="player.chapter()?.id ?? null"
          (selectChapter)="player.playChapter($event, 0, true)"
          (selectBookmark)="onSelectBookmark($event)"
          (deleteBookmark)="onDeleteBookmark(b.id, $event)"
          (closed)="drawerOpen.set(false)"
        />
      } @else {
        <div class="h-64 rounded-2xl bg-slate-200 dark:bg-surface-800 animate-pulse"></div>
      }
    </div>
  `
})
export class PlayerPageComponent implements OnInit {
  readonly player = inject(AudioPlayerService);
  readonly formatService = inject(AudioFormatService);
  private readonly bookmarkService = inject(BookmarkService);
  private readonly downloadManager = inject(DownloadManagerService);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);

  readonly bookId = input.required<string>();
  readonly bolum = input<string | undefined>(undefined);

  readonly t = T;
  readonly showText = signal(true);
  readonly drawerOpen = signal(false);
  readonly drawerTab = signal<'chapters' | 'bookmarks'>('chapters');
  readonly bookmarks = signal<BookmarkDto[]>([]);
  readonly addingBookmark = signal(false);
  bookmarkNote = '';

  ngOnInit(): void {
    const rawTime = this.route.snapshot.queryParamMap.get('t');
    const posSec = rawTime ? Number(rawTime) : undefined;
    void this.player.loadBook(this.bookId(), {
      chapterId: this.bolum(),
      positionSec: Number.isFinite(posSec) ? posSec : undefined,
      autoplay: true
    });
    this.loadBookmarks();
  }

  loadBookmarks(): void {
    this.bookmarkService.getByBook(this.bookId()).subscribe({
      next: (list) => this.bookmarks.set(list)
    });
  }

  openDrawer(tab: 'chapters' | 'bookmarks'): void {
    this.drawerTab.set(tab);
    this.drawerOpen.set(true);
  }

  saveBookmark(bookId: string): void {
    const ch = this.player.chapter();
    if (!ch) return;
    const sentence = this.player.currentSentence()?.t ?? null;
    const rawNote = this.bookmarkNote.trim();
    const note = rawNote || sentence || null;

    this.bookmarkService
      .create(bookId, {
        chapterId: ch.id,
        pozisyonSn: Math.round(this.player.position() * 10) / 10,
        not: note
      })
      .subscribe({
        next: (created) => {
          this.bookmarks.update((prev) => [...prev, created]);
          this.bookmarkNote = '';
          this.addingBookmark.set(false);
          this.toast.success('Yer imi eklendi.');
        }
      });
  }

  onSelectBookmark(bm: BookmarkDto): void {
    void this.player.playChapter(bm.chapterId, bm.pozisyonSn, true);
  }

  onDeleteBookmark(bookId: string, bookmarkId: string): void {
    this.bookmarkService.delete(bookId, bookmarkId).subscribe({
      next: () => {
        this.bookmarks.update((prev) => prev.filter((x) => x.id !== bookmarkId));
        this.toast.info('Yer imi silindi.');
      }
    });
  }

  downloadBook(bookId: string): void {
    void this.downloadManager.enqueueBook(bookId);
  }
}

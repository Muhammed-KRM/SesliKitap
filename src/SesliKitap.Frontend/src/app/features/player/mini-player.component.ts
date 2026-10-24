import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AudioFormatService } from '../../core/services/audio-format.service';
import { AudioPlayerService } from '../../core/services/audio-player.service';
import { CoverImageComponent } from '../../shared/components/cover-image.component';

@Component({
  selector: 'app-mini-player',
  standalone: true,
  imports: [RouterLink, CoverImageComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (shouldShow()) {
      @if (player.book(); as b) {
        <div
          class="fixed bottom-16 md:bottom-0 inset-x-0 z-30 bg-white/95 dark:bg-surface-900/95 backdrop-blur-md border-t border-slate-200 dark:border-slate-800 shadow-2xl"
        >
          <!-- İnce İlerleme Çizgisi -->
          <div class="h-1 w-full bg-slate-200 dark:bg-slate-800">
            <div
              class="h-full bg-brand-500 transition-all"
              [style.width.%]="progressPercent()"
            ></div>
          </div>

          <div class="max-w-7xl mx-auto px-4 h-16 flex items-center justify-between gap-3">
            <a
              [routerLink]="['/dinle', b.id]"
              class="flex items-center gap-3 min-w-0 flex-1 group"
            >
              <div class="w-10 h-12 rounded-lg overflow-hidden shrink-0">
                <app-cover-image [bookId]="b.id" [title]="b.baslik" [hasCover]="b.kapakVar" />
              </div>
              <div class="min-w-0">
                <p
                  class="text-sm font-bold text-slate-900 dark:text-slate-100 truncate group-hover:text-brand-400 transition"
                >
                  {{ b.baslik }}
                </p>
                <p class="text-xs text-slate-500 dark:text-slate-400 truncate">
                  @if (player.chapter(); as ch) {
                    <span>#{{ ch.siraNo }} · {{ ch.baslik }}</span>
                  }
                </p>
              </div>
            </a>

            <div class="flex items-center gap-2 shrink-0">
              <button
                type="button"
                (click)="player.toggle()"
                class="min-w-[44px] min-h-[44px] rounded-full bg-brand-500 hover:bg-brand-600 text-slate-950 font-bold flex items-center justify-center transition"
                [attr.aria-label]="player.isPlaying() ? 'Duraklat' : 'Oynat'"
              >
                {{ player.isPlaying() ? '⏸' : '▶' }}
              </button>
              <button
                type="button"
                (click)="player.skip(formatService.skipForwardSec())"
                class="min-w-[44px] min-h-[44px] px-2.5 rounded-xl bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-xs font-bold text-slate-700 dark:text-slate-200 transition"
                title="İleri sar"
              >
                ↻{{ formatService.skipForwardSec() }}
              </button>
            </div>
          </div>
        </div>
      }
    }
  `
})
export class MiniPlayerComponent {
  readonly player = inject(AudioPlayerService);
  readonly formatService = inject(AudioFormatService);
  private readonly router = inject(Router);

  readonly shouldShow = computed(() => {
    const hasBook = Boolean(this.player.book());
    const onFullPlayer = this.router.url.startsWith('/dinle/');
    return hasBook && !onFullPlayer;
  });

  readonly progressPercent = computed(() => {
    const dur = this.player.duration();
    if (dur <= 0) return 0;
    return Math.min(100, Math.max(0, (this.player.position() / dur) * 100));
  });
}

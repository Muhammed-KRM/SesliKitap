import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { T } from '../../core/i18n/tr';
import { SignalrService } from '../../core/services/signalr.service';
import { ThemeService } from '../../core/services/theme.service';

@Component({
  selector: 'app-top-bar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header
      class="sticky top-0 z-30 h-16 border-b border-slate-200 dark:border-slate-800 bg-white/85 dark:bg-surface-900/85 backdrop-blur-md px-4 md:px-6 flex items-center justify-between"
    >
      <div class="flex items-center gap-6">
        <a
          routerLink="/"
          class="flex items-center gap-2.5 font-bold text-lg tracking-tight text-slate-900 dark:text-slate-100"
        >
          <span
            class="w-8 h-8 rounded-lg bg-brand-500 text-slate-950 flex items-center justify-center font-serif font-extrabold"
          >
            S
          </span>
          <span>{{ t.app.title }}</span>
        </a>

        <nav class="hidden md:flex items-center gap-1 text-sm font-medium">
          <a
            routerLink="/"
            routerLinkActive="bg-brand-500/15 text-brand-400"
            [routerLinkActiveOptions]="{ exact: true }"
            class="px-3 py-2 rounded-lg text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition"
          >
            {{ t.nav.library }}
          </a>
          <a
            routerLink="/kesfet"
            routerLinkActive="bg-brand-500/15 text-brand-400"
            class="px-3 py-2 rounded-lg text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition"
          >
            {{ t.nav.discover }}
          </a>
          <a
            routerLink="/sesler"
            routerLinkActive="bg-brand-500/15 text-brand-400"
            class="px-3 py-2 rounded-lg text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition"
          >
            {{ t.nav.voices }}
          </a>
          <a
            routerLink="/isler"
            routerLinkActive="bg-brand-500/15 text-brand-400"
            class="px-3 py-2 rounded-lg text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition"
          >
            {{ t.nav.jobs }}
          </a>
          <a
            routerLink="/indirilenler"
            routerLinkActive="bg-brand-500/15 text-brand-400"
            class="px-3 py-2 rounded-lg text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition"
          >
            {{ t.nav.downloads }}
          </a>
          <a
            routerLink="/ayarlar"
            routerLinkActive="bg-brand-500/15 text-brand-400"
            class="px-3 py-2 rounded-lg text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition"
          >
            {{ t.nav.settings }}
          </a>
        </nav>
      </div>

      <div class="flex items-center gap-3">
        <div
          class="flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs border border-slate-200 dark:border-slate-800"
          [title]="connectionLabel()"
        >
          <span class="w-2 h-2 rounded-full" [class]="connectionDotClass()"></span>
          <span class="hidden sm:inline text-slate-600 dark:text-slate-400">
            {{ connectionLabel() }}
          </span>
        </div>

        <button
          type="button"
          (click)="theme.toggle()"
          class="min-w-[44px] min-h-[44px] rounded-xl flex items-center justify-center border border-slate-200 dark:border-slate-800 hover:bg-slate-100 dark:hover:bg-slate-800 transition"
          [attr.aria-label]="t.settings.theme"
        >
          {{ theme.mode() === 'dark' ? '☀' : '☾' }}
        </button>
      </div>
    </header>
  `
})
export class TopBarComponent {
  readonly theme = inject(ThemeService);
  readonly signalr = inject(SignalrService);
  readonly t = T;

  connectionLabel(): string {
    const s = this.signalr.status();
    if (s === 'connected') return 'Canlı';
    if (s === 'reconnecting') return 'Yeniden bağlanıyor...';
    return 'Çevrimdışı';
  }

  connectionDotClass(): string {
    const s = this.signalr.status();
    if (s === 'connected') return 'bg-emerald-400';
    if (s === 'reconnecting') return 'bg-amber-400 animate-pulse';
    return 'bg-rose-500';
  }
}

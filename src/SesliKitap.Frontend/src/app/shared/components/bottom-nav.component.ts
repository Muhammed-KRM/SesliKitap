import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { T } from '../../core/i18n/tr';

@Component({
  selector: 'app-bottom-nav',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav
      class="md:hidden fixed bottom-0 inset-x-0 z-30 h-16 bg-white/95 dark:bg-surface-900/95 backdrop-blur-md border-t border-slate-200 dark:border-slate-800 flex items-center justify-around safe-bottom"
    >
      <a
        routerLink="/"
        routerLinkActive="text-brand-400"
        [routerLinkActiveOptions]="{ exact: true }"
        class="flex flex-col items-center justify-center min-w-[48px] min-h-[44px] text-xs font-medium text-slate-500 dark:text-slate-400"
      >
        <span>📚</span>
        <span class="mt-0.5">{{ t.nav.library }}</span>
      </a>
      <a
        routerLink="/kesfet"
        routerLinkActive="text-brand-400"
        class="flex flex-col items-center justify-center min-w-[48px] min-h-[44px] text-xs font-medium text-slate-500 dark:text-slate-400"
      >
        <span>🔎</span>
        <span class="mt-0.5">{{ t.nav.discover }}</span>
      </a>
      <a
        routerLink="/sesler"
        routerLinkActive="text-brand-400"
        class="flex flex-col items-center justify-center min-w-[48px] min-h-[44px] text-xs font-medium text-slate-500 dark:text-slate-400"
      >
        <span>🎙</span>
        <span class="mt-0.5">{{ t.nav.voices }}</span>
      </a>
      <a
        routerLink="/isler"
        routerLinkActive="text-brand-400"
        class="flex flex-col items-center justify-center min-w-[48px] min-h-[44px] text-xs font-medium text-slate-500 dark:text-slate-400"
      >
        <span>⚙</span>
        <span class="mt-0.5">{{ t.nav.jobs }}</span>
      </a>
      <a
        routerLink="/ayarlar"
        routerLinkActive="text-brand-400"
        class="flex flex-col items-center justify-center min-w-[48px] min-h-[44px] text-xs font-medium text-slate-500 dark:text-slate-400"
      >
        <span>☰</span>
        <span class="mt-0.5">{{ t.nav.settings }}</span>
      </a>
    </nav>
  `
})
export class BottomNavComponent {
  readonly t = T;
}

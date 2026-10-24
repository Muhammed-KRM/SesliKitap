import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SwUpdate, VersionReadyEvent } from '@angular/service-worker';
import { filter } from 'rxjs';
import { AccessKeyService } from './core/services/access-key.service';
import { KeyboardShortcutsService } from './core/services/keyboard-shortcuts.service';
import { ProgressService } from './core/services/progress.service';
import { SignalrService } from './core/services/signalr.service';
import { ThemeService } from './core/services/theme.service';
import { ToastService } from './core/services/toast.service';
import { MiniPlayerComponent } from './features/player/mini-player.component';
import { BottomNavComponent } from './shared/components/bottom-nav.component';
import { ConfirmDialogComponent } from './shared/components/confirm-dialog.component';
import { ToastContainerComponent } from './shared/components/toast-container.component';
import { TopBarComponent } from './shared/components/top-bar.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    RouterOutlet,
    TopBarComponent,
    BottomNavComponent,
    MiniPlayerComponent,
    ToastContainerComponent,
    ConfirmDialogComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-screen flex flex-col bg-surface-50 dark:bg-surface-900">
      @if (accessKey.hasKey()) {
        <app-top-bar />
      }
      <main class="flex-1 pb-36 md:pb-20">
        <router-outlet />
      </main>
      @if (accessKey.hasKey()) {
        <app-mini-player />
        <app-bottom-nav />
      }
      <app-toast-container />
      <app-confirm-dialog />
    </div>
  `
})
export class AppComponent implements OnInit {
  readonly accessKey = inject(AccessKeyService);
  private readonly theme = inject(ThemeService);
  private readonly signalr = inject(SignalrService);
  private readonly shortcuts = inject(KeyboardShortcutsService);
  private readonly progressService = inject(ProgressService);
  private readonly swUpdate = inject(SwUpdate, { optional: true });
  private readonly toast = inject(ToastService);

  ngOnInit(): void {
    this.theme.init();
    this.shortcuts.init();

    if (this.accessKey.hasKey()) {
      void this.signalr.connect();
      void this.progressService.flushPending();
    }

    if (typeof window !== 'undefined') {
      window.addEventListener('online', () => {
        if (this.accessKey.hasKey()) {
          void this.progressService.flushPending();
        }
      });
    }

    if (this.swUpdate?.isEnabled) {
      this.swUpdate.versionUpdates
        .pipe(filter((evt): evt is VersionReadyEvent => evt.type === 'VERSION_READY'))
        .subscribe(() => {
          this.toast.info('Yeni sürüm hazır.', undefined, {
            label: 'Yenile',
            callback: () => window.location.reload()
          });
        });
    }
  }
}

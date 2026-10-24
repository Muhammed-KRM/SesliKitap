import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AccessKeyService } from './core/services/access-key.service';
import { SignalrService } from './core/services/signalr.service';
import { ThemeService } from './core/services/theme.service';
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
    ToastContainerComponent,
    ConfirmDialogComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-screen flex flex-col bg-surface-50 dark:bg-surface-900">
      @if (accessKey.hasKey()) {
        <app-top-bar />
      }
      <main class="flex-1 pb-20 md:pb-8">
        <router-outlet />
      </main>
      @if (accessKey.hasKey()) {
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

  ngOnInit(): void {
    this.theme.init();
    if (this.accessKey.hasKey()) {
      void this.signalr.connect();
    }
  }
}

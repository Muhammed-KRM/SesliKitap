import { Routes } from '@angular/router';
import { accessGuard } from './core/guards/access.guard';

export const routes: Routes = [
  {
    path: 'erisim',
    loadComponent: () =>
      import('./features/access/access-page.component').then((m) => m.AccessPageComponent)
  },
  {
    path: '',
    canActivate: [accessGuard],
    loadComponent: () =>
      import('./features/library/library-page.component').then((m) => m.LibraryPageComponent)
  },
  {
    path: 'kitap/:id',
    canActivate: [accessGuard],
    loadComponent: () =>
      import('./features/book-detail/book-detail-page.component').then(
        (m) => m.BookDetailPageComponent
      )
  },
  {
    path: 'dinle/:bookId',
    canActivate: [accessGuard],
    loadComponent: () =>
      import('./features/player/player-page.component').then((m) => m.PlayerPageComponent)
  },
  {
    path: 'sesler',
    canActivate: [accessGuard],
    loadComponent: () =>
      import('./features/voices/voices-page.component').then((m) => m.VoicesPageComponent)
  },
  {
    path: 'isler',
    canActivate: [accessGuard],
    loadComponent: () =>
      import('./features/jobs/jobs-page.component').then((m) => m.JobsPageComponent)
  },
  {
    path: 'indirilenler',
    loadComponent: () =>
      import('./features/downloads/downloads-page.component').then(
        (m) => m.DownloadsPageComponent
      )
  },
  {
    path: 'kesfet',
    canActivate: [accessGuard],
    loadComponent: () =>
      import('./features/discover/discover-page.component').then((m) => m.DiscoverPageComponent)
  },
  {
    path: 'ayarlar',
    canActivate: [accessGuard],
    loadComponent: () =>
      import('./features/settings/settings-page.component').then((m) => m.SettingsPageComponent)
  },
  {
    path: '**',
    redirectTo: ''
  }
];

import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-player-page',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="max-w-4xl mx-auto px-4 py-8 text-center space-y-4">
      <h1 class="text-xl font-bold">Sesli Kitap Oynatıcı ({{ bookId() }})</h1>
      <a routerLink="/" class="text-brand-400 underline text-sm">Kütüphaneye Dön</a>
    </div>
  `
})
export class PlayerPageComponent {
  readonly bookId = input.required<string>();
}

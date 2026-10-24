import { ChangeDetectionStrategy, Component } from '@angular/core';
import { EmptyStateComponent } from '../../shared/components/empty-state.component';

@Component({
  selector: 'app-discover-page',
  standalone: true,
  imports: [EmptyStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="max-w-6xl mx-auto px-4 md:px-6 py-6 space-y-6">
      <h1 class="text-2xl font-bold">Kitap Keşfet</h1>
      <app-empty-state
        title="Açık Kaynak Kitap Arama"
        description="Internet Archive, Open Library ve Project Gutenberg kataloglarında kitap arayın."
      />
    </div>
  `
})
export class DiscoverPageComponent {}

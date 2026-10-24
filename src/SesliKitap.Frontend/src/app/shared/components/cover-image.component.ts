import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal
} from '@angular/core';
import { MediaTokenService } from '../../core/services/media-token.service';

const PALETTE = [
  'from-amber-600 to-orange-800',
  'from-emerald-600 to-teal-800',
  'from-sky-600 to-indigo-800',
  'from-purple-600 to-fuchsia-800',
  'from-rose-600 to-pink-800',
  'from-cyan-600 to-blue-800'
];

@Component({
  selector: 'app-cover-image',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="relative w-full h-full overflow-hidden rounded-lg bg-slate-800 select-none">
      @if (resolvedUrl() && !loadFailed()) {
        <img
          [src]="resolvedUrl()"
          [alt]="title()"
          loading="lazy"
          (error)="onImgError()"
          class="w-full h-full object-cover"
        />
      } @else {
        <div
          class="w-full h-full flex flex-col items-center justify-center p-3 text-center bg-gradient-to-br text-white"
          [class]="gradientClass()"
        >
          <span class="text-2xl font-serif font-bold tracking-wider opacity-95">
            {{ initials() }}
          </span>
          <span class="mt-1 text-[11px] leading-tight line-clamp-2 opacity-80 font-medium">
            {{ title() }}
          </span>
        </div>
      }
    </div>
  `
})
export class CoverImageComponent {
  private readonly mediaToken = inject(MediaTokenService);

  readonly bookId = input.required<string>();
  readonly title = input<string>('Kitap');
  readonly hasCover = input<boolean>(false);

  readonly resolvedUrl = signal<string | null>(null);
  readonly loadFailed = signal<boolean>(false);

  readonly initials = computed(() => {
    const words = (this.title() || 'SK').trim().split(/\s+/).slice(0, 2);
    return words.map((w) => w.charAt(0).toLocaleUpperCase('tr-TR')).join('');
  });

  readonly gradientClass = computed(() => {
    const t = this.title() || '';
    let hash = 0;
    for (let i = 0; i < t.length; i++) {
      hash = (hash * 31 + t.charCodeAt(i)) | 0;
    }
    return PALETTE[Math.abs(hash) % PALETTE.length];
  });

  constructor() {
    effect(
      () => {
        const id = this.bookId();
        const has = this.hasCover();
        this.loadFailed.set(false);
        if (!id || !has) {
          this.resolvedUrl.set(null);
          return;
        }
        void this.mediaToken.coverUrl(id).then((url) => this.resolvedUrl.set(url));
      },
      { allowSignalWrites: true }
    );
  }

  onImgError(): void {
    this.loadFailed.set(true);
  }
}

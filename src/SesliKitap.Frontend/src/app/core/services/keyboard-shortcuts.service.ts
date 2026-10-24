import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AudioFormatService } from './audio-format.service';
import { AudioPlayerService } from './audio-player.service';

@Injectable({ providedIn: 'root' })
export class KeyboardShortcutsService {
  private readonly player = inject(AudioPlayerService);
  private readonly formatService = inject(AudioFormatService);
  private readonly router = inject(Router);
  private initialized = false;

  init(): void {
    if (this.initialized || typeof window === 'undefined') {
      return;
    }
    this.initialized = true;

    window.addEventListener('keydown', (event: KeyboardEvent) => {
      const target = event.target as HTMLElement | null;
      if (
        target &&
        (target.tagName === 'INPUT' ||
          target.tagName === 'TEXTAREA' ||
          target.tagName === 'SELECT' ||
          target.isContentEditable)
      ) {
        return;
      }

      if (!this.player.book()) {
        return;
      }

      switch (event.key) {
        case ' ':
          event.preventDefault();
          this.player.toggle();
          break;
        case 'ArrowLeft':
          event.preventDefault();
          if (event.shiftKey) {
            void this.player.prevChapter();
          } else {
            this.player.skip(-this.formatService.skipBackSec());
          }
          break;
        case 'ArrowRight':
          event.preventDefault();
          if (event.shiftKey) {
            void this.player.nextChapter();
          } else {
            this.player.skip(this.formatService.skipForwardSec());
          }
          break;
        case '[':
          event.preventDefault();
          this.player.setRate(this.player.rate() - 0.25);
          break;
        case ']':
          event.preventDefault();
          this.player.setRate(this.player.rate() + 0.25);
          break;
        case 'm':
        case 'M': {
          event.preventDefault();
          const currentBook = this.player.book();
          if (!currentBook) break;
          if (this.router.url.startsWith('/dinle/')) {
            void this.router.navigate(['/kitap', currentBook.id]);
          } else {
            void this.router.navigate(['/dinle', currentBook.id]);
          }
          break;
        }
      }
    });
  }
}

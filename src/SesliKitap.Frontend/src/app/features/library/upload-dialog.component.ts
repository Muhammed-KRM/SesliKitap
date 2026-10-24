import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  input,
  output,
  signal
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { T } from '../../core/i18n/tr';
import { BookDetail } from '../../core/models/book.model';
import { BookService } from '../../core/services/book.service';
import { ToastService } from '../../core/services/toast.service';
import { ProgressBarComponent } from '../../shared/components/progress-bar.component';
import { DropZoneDirective } from '../../shared/directives/drop-zone.directive';
import { FileSizePipe } from '../../shared/pipes/file-size.pipe';

const MAX_BYTES = 300 * 1024 * 1024;
const ALLOWED_EXTS = ['.pdf', '.epub', '.txt'];

@Component({
  selector: 'app-upload-dialog',
  standalone: true,
  imports: [FormsModule, DropZoneDirective, ProgressBarComponent, FileSizePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (open()) {
      <div
        class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-xs p-0 sm:p-4"
        role="dialog"
        aria-modal="true"
      >
        <div
          class="w-full h-full sm:h-auto sm:max-w-lg sm:rounded-2xl bg-white dark:bg-surface-800 border border-slate-200 dark:border-slate-700 p-6 flex flex-col justify-between shadow-2xl"
        >
          <div>
            <div class="flex items-center justify-between">
              <h2 class="text-lg font-bold text-slate-900 dark:text-slate-100">
                {{ t.upload.title }}
              </h2>
              <button
                type="button"
                (click)="close()"
                class="min-w-[44px] min-h-[44px] rounded-xl flex items-center justify-center text-slate-400 hover:text-slate-200"
              >
                ✕
              </button>
            </div>

            <div
              appDropZone
              (dropped)="onFilesDropped($event)"
              (click)="fileInput.click()"
              class="mt-4 border-2 border-dashed border-slate-300 dark:border-slate-700 hover:border-brand-500 rounded-2xl p-6 text-center cursor-pointer transition"
            >
              <input
                #fileInput
                type="file"
                accept=".pdf,.epub,.txt"
                class="hidden"
                (change)="onFileSelected($event)"
              />
              @if (selectedFile(); as f) {
                <p class="text-sm font-semibold text-brand-400 break-all">{{ f.name }}</p>
                <p class="mt-1 text-xs text-slate-400">{{ f.size | fileSize }}</p>
              } @else {
                <p class="text-sm font-medium text-slate-700 dark:text-slate-200">
                  {{ t.upload.dropArea }}
                </p>
                <p class="mt-1 text-xs text-slate-500 dark:text-slate-400">
                  {{ t.upload.maxSizeHint }}
                </p>
              }
            </div>

            <div class="mt-4 space-y-3">
              <div>
                <label class="block text-xs font-medium text-slate-500 dark:text-slate-400 mb-1">
                  {{ t.upload.titleLabel }}
                </label>
                <input
                  type="text"
                  [(ngModel)]="customTitle"
                  class="w-full px-3.5 py-2.5 rounded-xl bg-slate-50 dark:bg-surface-900 border border-slate-300 dark:border-slate-700 text-sm"
                />
              </div>
              <div>
                <label class="block text-xs font-medium text-slate-500 dark:text-slate-400 mb-1">
                  {{ t.upload.authorLabel }}
                </label>
                <input
                  type="text"
                  [(ngModel)]="customAuthor"
                  class="w-full px-3.5 py-2.5 rounded-xl bg-slate-50 dark:bg-surface-900 border border-slate-300 dark:border-slate-700 text-sm"
                />
              </div>
            </div>

            @if (uploading()) {
              <div class="mt-4 space-y-1.5">
                <div class="flex justify-between text-xs text-slate-400">
                  <span>{{ t.upload.uploading }}</span>
                  <span>%{{ progress() }}</span>
                </div>
                <app-progress-bar [value]="progress()" />
              </div>
            }

            @if (errorMsg()) {
              <div
                class="mt-4 p-3 rounded-xl bg-rose-500/10 border border-rose-500/30 text-rose-400 text-xs flex items-center justify-between gap-2"
              >
                <span>{{ errorMsg() }}</span>
                @if (duplicateBookId()) {
                  <button
                    type="button"
                    (click)="goToDuplicate()"
                    class="px-3 py-1.5 rounded-lg bg-brand-500 text-slate-950 font-semibold whitespace-nowrap"
                  >
                    {{ t.upload.goToBook }}
                  </button>
                }
              </div>
            }
          </div>

          <div class="mt-6 flex items-center justify-end gap-3">
            <button
              type="button"
              (click)="close()"
              [disabled]="uploading()"
              class="px-4 py-2.5 min-h-[44px] rounded-xl text-sm font-medium text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-700/50"
            >
              {{ t.common.cancel }}
            </button>
            <button
              type="button"
              (click)="submit()"
              [disabled]="!selectedFile() || uploading()"
              class="px-5 py-2.5 min-h-[44px] rounded-xl bg-brand-500 hover:bg-brand-600 disabled:opacity-50 text-slate-950 font-semibold text-sm transition"
            >
              {{ uploading() ? t.upload.uploading : t.upload.submit }}
            </button>
          </div>
        </div>
      </div>
    }
  `
})
export class UploadDialogComponent {
  private readonly bookService = inject(BookService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  readonly open = input<boolean>(false);
  readonly initialFile = input<File | null>(null);
  readonly closed = output<void>();
  readonly uploaded = output<BookDetail>();

  readonly t = T;
  readonly selectedFile = signal<File | null>(null);
  readonly uploading = signal(false);
  readonly progress = signal(0);
  readonly errorMsg = signal<string | null>(null);
  readonly duplicateBookId = signal<string | null>(null);

  customTitle = '';
  customAuthor = '';

  constructor() {
    effect(
      () => {
        const f = this.initialFile();
        if (f) {
          this.validateAndSetFile(f);
        }
      },
      { allowSignalWrites: true }
    );
  }

  onFilesDropped(files: File[]): void {
    if (files.length > 0) {
      this.validateAndSetFile(files[0]);
    }
  }

  onFileSelected(event: Event): void {
    const inputEl = event.target as HTMLInputElement;
    if (inputEl.files && inputEl.files.length > 0) {
      this.validateAndSetFile(inputEl.files[0]);
    }
  }

  private validateAndSetFile(file: File): void {
    this.errorMsg.set(null);
    this.duplicateBookId.set(null);

    const lower = file.name.toLowerCase();
    const validExt = ALLOWED_EXTS.some((ext) => lower.endsWith(ext));
    if (!validExt) {
      this.errorMsg.set(T.upload.invalidExtension);
      return;
    }

    if (file.size > MAX_BYTES) {
      this.errorMsg.set(T.upload.tooLarge);
      return;
    }

    this.selectedFile.set(file);
  }

  submit(): void {
    const file = this.selectedFile();
    if (!file || this.uploading()) {
      return;
    }

    this.uploading.set(true);
    this.progress.set(0);
    this.errorMsg.set(null);
    this.duplicateBookId.set(null);

    this.bookService
      .upload(file, { baslik: this.customTitle, yazar: this.customAuthor })
      .subscribe({
        next: (state) => {
          this.progress.set(state.yuzde);
          if (state.tamamlandi && state.sonuc) {
            this.uploading.set(false);
            this.toast.success('Kitap yüklendi, metin işleme başlatıldı.');
            this.uploaded.emit(state.sonuc);
            this.close();
            void this.router.navigate(['/kitap', state.sonuc.id]);
          }
        },
        error: (err: HttpErrorResponse) => {
          this.uploading.set(false);
          const problem = err.error as
            | { detail?: string; mevcutKitapId?: string; existingBookId?: string }
            | undefined;
          if (err.status === 409) {
            this.errorMsg.set(problem?.detail || T.upload.duplicateTitle);
            const dupId = problem?.mevcutKitapId ?? problem?.existingBookId ?? null;
            this.duplicateBookId.set(dupId);
          } else {
            this.errorMsg.set(problem?.detail || 'Yükleme sırasında bir hata oluştu.');
          }
        }
      });
  }

  goToDuplicate(): void {
    const id = this.duplicateBookId();
    if (id) {
      this.close();
      void this.router.navigate(['/kitap', id]);
    }
  }

  close(): void {
    this.selectedFile.set(null);
    this.customTitle = '';
    this.customAuthor = '';
    this.errorMsg.set(null);
    this.duplicateBookId.set(null);
    this.closed.emit();
  }
}

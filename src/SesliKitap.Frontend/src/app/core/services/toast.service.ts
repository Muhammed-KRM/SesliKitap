import { Injectable, signal } from '@angular/core';

export type ToastType = 'basari' | 'hata' | 'bilgi' | 'uyari';

export interface ToastAction {
  label: string;
  callback: () => void;
}

export interface ToastMessage {
  id: string;
  type: ToastType;
  title: string;
  detail?: string;
  action?: ToastAction;
  durationMs: number;
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  readonly toasts = signal<ToastMessage[]>([]);
  private counter = 0;

  show(
    type: ToastType,
    title: string,
    opts?: { detail?: string; action?: ToastAction; durationMs?: number }
  ): string {
    const id = `toast-${++this.counter}-${Date.now()}`;
    const durationMs = opts?.durationMs ?? 4000;
    const item: ToastMessage = {
      id,
      type,
      title,
      detail: opts?.detail,
      action: opts?.action,
      durationMs
    };

    this.toasts.update((list) => [...list, item]);

    if (durationMs > 0) {
      setTimeout(() => this.dismiss(id), durationMs);
    }
    return id;
  }

  success(title: string, detail?: string, action?: ToastAction): string {
    return this.show('basari', title, { detail, action });
  }

  error(title: string, detail?: string, action?: ToastAction): string {
    return this.show('hata', title, { detail, action, durationMs: 6000 });
  }

  info(title: string, detail?: string, action?: ToastAction): string {
    return this.show('bilgi', title, { detail, action });
  }

  warning(title: string, detail?: string, action?: ToastAction): string {
    return this.show('uyari', title, { detail, action, durationMs: 5000 });
  }

  dismiss(id: string): void {
    this.toasts.update((list) => list.filter((t) => t.id !== id));
  }

  clear(): void {
    this.toasts.set([]);
  }
}
